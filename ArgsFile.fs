module Scripts.ArgsFile

open System
open System.IO
open System.Runtime.CompilerServices
open System.Text
open System.Text.RegularExpressions
open FSharp.Compiler.CodeAnalysis
open Ionide.ProjInfo
open Ionide.ProjInfo.Types
open Microsoft.Build.Logging.StructuredLogger
open CliWrap
open Serilog

[<RequireQualifiedAccess>]
type OtherOption =
    // Eg. '--test:DumpGraph'
    | TestFlag of string
    // Eg. '--deterministic+'
    | Bool of string * bool
    // Eg. '--warn:3'
    | KeyValue of string * string
    // Eg. '--times'
    | Simple of string

type Define = string
type Reference = string
type Input = string

[<RequireQualifiedAccess>]
type FscArg =
    // Eg. '--define:DEBUG'
    | Define of Define
    // Eg. '-r:C:\...\System.dll'
    | Reference of Reference
    // Eg. 'Internals.fs'
    | Input of Input
    | OtherOption of OtherOption

type FscArgs = FscArg[]

type SArgs =
    {
        OtherOptions : OtherOption list
        Defines : Define list
        Refs : Reference list
        Inputs : Input list
    }
    with member this.Output =
            this.OtherOptions
            |> List.choose (function
                | OtherOption.KeyValue("-o", output) -> Some output
                | _ -> None
            )
            |> List.exactlyOne

module FscArgs =
    let parseSingle (arg : string) =
        match Regex.Match(arg, "^--define\:(.+)$") with
        | m when m.Success ->
            m.Groups[1].Value
            |> FscArg.Define
        | _ ->
        match Regex.Match(arg, "^--([\d\w_]+)([\+\-])$") with
        | m when m.Success ->
            let flag = m.Groups[1].Value.ToLower()
            let value = match m.Groups[2].Value with "+" -> true | "-" -> false | _ -> failwith $"Unexpected group value for bool arg: {m}"
            (flag, value)
            |> OtherOption.Bool
            |> FscArg.OtherOption
        | _ ->
        match Regex.Match(arg, "^-r:(.+)$") with
        | m when m.Success ->
            let path = m.Groups[1].Value
            path
            |> FscArg.Reference
        | _ ->
        match Regex.Match(arg, "^--test:(.+)$") with
        | m when m.Success ->
            let name = m.Groups[1].Value
            name
            |> OtherOption.TestFlag
            |> FscArg.OtherOption
        | _ ->
        match Regex.Match(arg, "^(-?-[\d\w_]+)\:(.+)$") with
        | m when m.Success ->
            let flag = m.Groups[1].Value
            let value = m.Groups[2].Value
            (flag, value)
            |> OtherOption.KeyValue
            |> FscArg.OtherOption
        | _ ->
        match Regex.Match(arg, "^[\d\w_].+$") with
        | m when m.Success ->
            FscArg.Input m.Groups[0].Value
        | _ ->
        match Regex.Match(arg, "^-?-[^:]+$") with
        | m when m.Success ->
            let flag = m.Groups[0].Value
            flag
            |> OtherOption.Simple
            |> FscArg.OtherOption
        | _ ->
            failwith $"Unable to parser FSC arg '{arg}'"
        
    let stringify (arg : FscArg) =
        match arg with
        | FscArg.Define name -> $"--define:{name}"
        | FscArg.Reference path -> $"-r:{path}"
        | FscArg.Input input -> $"{input}"
        | FscArg.OtherOption optionString ->
            match optionString with
            | OtherOption.Bool(name, value) ->
                let vString = if value then "+" else "-" 
                $"--{name}{vString}"
            | OtherOption.TestFlag name -> $"--test:{name}"
            | OtherOption.KeyValue(key, value) -> $"{key}:{value}"
            | OtherOption.Simple simpleString -> $"{simpleString}"
        
    let split (argsString : string) : string[] =
        argsString.Split("\r\n")
        |> Array.collect (fun s -> s.Split("\n"))
        
    let parse (args : string[]) : FscArgs =
        args
        |> Array.map parseSingle
    
    let stringifyAll (args : FscArgs) : string[] =
        args
        |> Array.map stringify

module SArgs =
    let structurize (args : FscArg seq) : SArgs =
        let args = args |> Seq.toList
        {
            SArgs.OtherOptions = args |> List.choose (function | FscArg.OtherOption otherOption -> Some otherOption | _ -> None)
            SArgs.Defines = args |> List.choose (function | FscArg.Define d -> Some d | _ -> None)
            SArgs.Refs = args |> List.choose (function | FscArg.Reference ref -> Some ref | _ -> None)
            SArgs.Inputs = args |> List.choose (function | FscArg.Input input -> Some input | _ -> None)
        }
    
    let destructurize (args : SArgs) : FscArgs =
        seq {
            yield! (args.OtherOptions |> List.map FscArg.OtherOption)
            yield! (args.Defines |> List.map FscArg.Define)
            yield! (args.Refs |> List.map FscArg.Reference)
            yield! (args.Inputs |> List.map FscArg.Input)
        }
        |> Seq.toArray
    
    let ofFile (argsFile : string) =
        File.ReadAllLines(argsFile)
        |> FscArgs.parse
        |> structurize
    
    let toFile (argsFile : string) (args : SArgs) =
        args
        |> destructurize
        |> FscArgs.stringifyAll
        |> fun args -> File.WriteAllLines(argsFile, args)
    
    let limitInputsCount (n : int) (args : SArgs) : SArgs =
        {
            args with
                Inputs = args.Inputs |> List.take (min args.Inputs.Length n)
        }
    
    let limitInputsToSpecificInput (lastInput : string) (args : SArgs) : SArgs =
        {
            args with
                Inputs = args.Inputs |> List.takeWhile (fun l -> l <> lastInput)
        }
    
    let setOption (matcher : OtherOption -> bool) (value : OtherOption option) (args : SArgs) : SArgs =
        let opts = args.OtherOptions
        let opts, found =
            opts
            |> List.mapFold (fun (found : bool) opt ->
                if matcher opt then value, true
                else Some opt, found
            ) false
        let opts = opts |> List.choose id
        let opts =
            match found, value with
            | true, _
            | false, None -> opts
            | false, Some value -> value :: opts
        {
            args with
                OtherOptions = opts
        }
    
    let clearOption (matcher : OtherOption -> bool) (args : SArgs) : SArgs =
        setOption matcher None args
    
    let setOutput (output : string) (args : SArgs) : SArgs =
        setOption
            (function OtherOption.KeyValue("-o", _) -> true | _ -> false)
            (OtherOption.KeyValue("-o", output) |> Some)
            args
    
    let setBool (name : string) (value : bool) (args : SArgs) : SArgs =
        setOption
            (function OtherOption.Bool(s, _) when s = name -> true | _ -> false)
            (OtherOption.Bool(name, value) |> Some)
            args
    
    let setTestFlag (name : string) (enable : bool) (args : SArgs) : SArgs =
        setOption
            (function OtherOption.TestFlag s when s = name -> true | _ -> false)
            (if enable then OtherOption.TestFlag(name) |> Some else None)
            args
    
    let clearTestFlag (name : string) (args : SArgs) : SArgs =
        setTestFlag name false args
        
    let setKeyValue (name : string) (value : string) (args : SArgs) : SArgs =
        setOption
            (function OtherOption.KeyValue(k, v) when k.Equals(name, StringComparison.OrdinalIgnoreCase) -> true | _ -> false)
            (OtherOption.KeyValue(name, value) |> Some)
            args

/// Create a text file with the F# compiler arguments scrapped from a binary log file.
/// Run `dotnet build --no-incremental -bl` to create the binlog file.
/// The --no-incremental flag is essential for this scraping code.
let mkCompilerArgsFromBinLog file =
    let build = BinaryLog.ReadBuild file

    let projectName =
        build.Children
        |> Seq.choose (
            function
            | :? Project as p -> Some p.Name
            | _ -> None
        )
        |> Seq.distinct
        |> Seq.exactlyOne

    let message (fscTask: FscTask) =
        fscTask.Children
        |> Seq.tryPick (
            function
            | :? Message as m when m.Text.Contains "fsc" -> Some m.Text
            | _ -> None
        )

    let mutable args = None

    build.VisitAllChildren<Task>(fun task ->
        match task with
        | :? FscTask as fscTask ->
            match fscTask.Parent.Parent with
            | :? Project as p when p.Name = projectName -> args <- message fscTask
            | _ -> ()
        | _ -> ()
    )

    match args with
    | None -> failwith "Could not find fsc commandline args in the MSBuild binlog file. Did you build using '--no-incremental'?"
    | Some args ->
        match args.IndexOf "-o:" with
        | -1 -> failwith "Args text does not look like F# compiler args"
        | idx -> args.Substring(idx)

type Command with
    member this.ExecuteAssertSuccess() =
        let res =
            this
                // .WithStandardOutputPipe(PipeTarget.ToFile("stdout.txt"))
                // .WithStandardErrorPipe(PipeTarget.ToFile("stderr.txt"))
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToFile("c:/projekty/fsharp/fsharp_scripts/stdout.txt"))
                .WithStandardErrorPipe(PipeTarget.ToFile("c:/projekty/fsharp/fsharp_scripts/stderr.txt"))
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult()
        if res.ExitCode <> 0 then
            failwith $"Non-zero exit code for command '{this.TargetFilePath} {this.Arguments}'"
        else
            ()
[<MethodImpl(MethodImplOptions.NoInlining)>]
let private doLoadOptions (projectPath : string) =
    let toolsPath = Init.init (DirectoryInfo(Path.GetDirectoryName(projectPath))) None
    // TODO allow customization of build properties
    let props = []
    let loader = WorkspaceLoader.Create (toolsPath, props)

    let projects =
        loader.LoadProjects ([projectPath], [], BinaryLogGeneration.Within (DirectoryInfo("c:/projekty/fsharp/fsharp_scripts"))) |> Seq.toList

    match projects with
    | [project] ->
        Log.Information("Loaded project options from {projectPath}", projectPath)
        let fsOptions = FCS.mapToFSharpProjectOptions project []
        fsOptions
    | _ ->
        failwith $"No projects were loaded from {projectPath} - this indicates an error in cracking the projects."
    
let convertOptionsToArgs (options : FSharpProjectOptions) : SArgs =
    let n = Path.GetFileNameWithoutExtension(options.ProjectFileName)
    seq {
        yield! options.OtherOptions
        yield! options.SourceFiles
    }
    |> Seq.toArray
    |> FscArgs.parse
    |> SArgs.structurize

// TODO Allow using configurations other than plain 'dotnet build'.
let mkArgsFileProjInfo projectPath argsFile =
    doLoadOptions projectPath
    |> convertOptionsToArgs
    |> SArgs.toFile argsFile
