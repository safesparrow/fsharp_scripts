module Scripts.ArgsFile

open System
open System.IO
open System.Text.RegularExpressions
open Microsoft.Build.Logging.StructuredLogger
open CliWrap

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

type StructuredArgs =
    {
        OtherOptions : OtherOption list
        Defines : Define list
        Refs : Reference list
        Inputs : Input list
    }

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
        match Regex.Match(arg, "^(-?-[\d\w_]+)\:(.+)$") with
        | m when m.Success ->
            let flag = m.Groups[1].Value.ToLower()
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

module StructuredArgs =
    let structurize (args : FscArg seq) : StructuredArgs =
        let args = args |> Seq.toList
        {
            StructuredArgs.OtherOptions = args |> List.choose (function | FscArg.OtherOption otherOption -> Some otherOption | _ -> None)
            StructuredArgs.Defines = args |> List.choose (function | FscArg.Define d -> Some d | _ -> None)
            StructuredArgs.Refs = args |> List.choose (function | FscArg.Reference ref -> Some ref | _ -> None)
            StructuredArgs.Inputs = args |> List.choose (function | FscArg.Input input -> Some input | _ -> None)
        }
    
    let destructurize (args : StructuredArgs) : FscArgs =
        seq {
            yield! (args.OtherOptions |> List.map FscArg.OtherOption)
            yield! (args.Defines |> List.map FscArg.Define)
            yield! (args.Refs |> List.map FscArg.Reference)
            yield! (args.Inputs |> List.map FscArg.Input)
        }
        |> Seq.toArray
    
    let limitInputsCount (n : int) (args : StructuredArgs) : StructuredArgs =
        {
            args with
                Inputs = args.Inputs |> List.take (max args.Inputs.Length n)
        }
    
    let limitInputsToSpecificInput (lastInput : string) (args : StructuredArgs) : StructuredArgs =
        {
            args with
                Inputs = args.Inputs |> List.takeWhile (fun l -> l <> lastInput)
        }
    
    let setOption (matcher : OtherOption -> bool) (value : OtherOption option) (args : StructuredArgs) : StructuredArgs =
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
    
    let setBool (name : string) (value : bool) (args : StructuredArgs) : StructuredArgs =
        setOption
            (function OtherOption.Bool(s, _) when s = name -> true | _ -> false)
            (OtherOption.Bool(name, value) |> Some)
            args
    
    let setTestFlag (name : string) (args : StructuredArgs) : StructuredArgs =
        setOption
            (function OtherOption.TestFlag s when s = name -> true | _ -> false)
            (OtherOption.TestFlag(name) |> Some)
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

let mkArgsFile projectPath argsFile =
    if not (File.Exists projectPath) then
        failwithf $"%s{projectPath} does not exist"

    if not (projectPath.EndsWith(".fsproj")) then
        failwithf $"%s{projectPath} is not an fsharp project file"

    let binLogFile = $"{Path.GetTempFileName()}.binlog"
    File.Delete(binLogFile)
    printfn $"Building '{projectPath}' and creating binlog file: '{binLogFile}'"

    Cli
        .Wrap("dotnet")
        .WithArguments($"build {projectPath} -bl:{binLogFile} --no-incremental -p:BuildProjectReferences=false")
        .ExecuteAsync()
        .Task.Wait()

    let args = mkCompilerArgsFromBinLog binLogFile
    File.Delete(binLogFile)
    File.WriteAllText(argsFile, args)
    printfn $"Args written to: '{argsFile}'"
    
let getArgsPath projectPath =
    let directory = FileInfo(projectPath).Directory.FullName
    Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(projectPath)}.args")
