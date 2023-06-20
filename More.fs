module Scripts.More

open System
open System.IO
open Ionide.ProjInfo
open Ionide.ProjInfo.Types
open Newtonsoft.Json
open Scripts
open Scripts.ArgsFile
open Scripts.Sample
open Newtonsoft.Json.Linq
open Serilog
open Scripts.DeterminismExtracts

/// Find the lowest number x in [a, b] for which `f x = true`, or None if it doesn't exist 
let rec binSearchLowestTrueValue (a : int) (b : int) (f : int -> bool) =
    Log.Information("binSearch {range}", $"[{a}, {b}]")
    let f (x : int) =
        Log.Information("binSearch evaluation for {idx}", x)
        f x 
    if a = b then (if f a then Some a else None) else
    if a >= b then Some a
    else
        let m = (a + b + 1) / 2
        let res = f m
        if res then binSearchLowestTrueValue m b f
        else binSearchLowestTrueValue a (m-1) f

let binSearchArrayIndices<'a> (f : 'a -> bool) (queryItems : 'a[]) =
    let g i = f (queryItems[i])
    binSearchLowestTrueValue 0 (queryItems.Length-1) g
    |> Option.map (fun i -> queryItems[i])

type ProjectWithArgs =
    {
        /// fsproj path
        Project : string
        Args : SArgs
    }

[<AutoOpen>]
module Samples =
    let fantomas =
        {
            Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "fantomas", "18f31541e983c9301e6a55ba6582817bc704cb6f")
            PrepareScript = PrepareScript.JustBuild
            // PrepareScript = PrepareScript.PowerShell "echo 'Fantomas'"
        }
    
    let fsharp =
        {
            Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("dotnet", "fsharp", "409168556aed9c6ec8595d1d9525fb5b88888fc4")
            PrepareScript = PrepareScript.PowerShell "./Build.cmd -noVisualStudio"
        }
    
    let determinism =
        {
            Sample.CodebaseSpec = CodebaseSpec.Local (Path.Combine(__SOURCE_DIRECTORY__, "../DeterminismSample/"))
            PrepareScript = PrepareScript.JustBuild
        }

let getJsonTokenFromFile (path : string) (jpath : string) =
    let json = File.ReadAllText(path)
    let o = JObject.Parse(json)
    let x = o.SelectToken(jpath)
    if x = null then None
    else x.Value<string>() |> Some
    
let getPlausibleSourceListPrefixEndIndices (inputs : Input list) =
    inputs
    |> List.indexed
    |> List.filter (fun (_, input) -> input.EndsWith(".fs"))
    |> List.map fst
    |> List.toArray

let getOutputType (args : SArgs) =
    args
    |> SArgs.destructurize
    |> FscArgs.stringifyAll
    |> Array.toList
    |> FscArguments.outType

let getAllPlausibleArgsWithShortenedSourceListForLibrary (args : SArgs) =
    match getOutputType args with
    | ProjectOutputType.Exe -> failwith "Exe projects cannot have their source list shortened because the last source file is required as it represents the entry point."
    | _ ->
        let indices = getPlausibleSourceListPrefixEndIndices args.Inputs
        indices
        |> Array.map (fun lastSourceIdx ->
            args
            |> SArgs.limitInputsCount (lastSourceIdx+1)
        )

/// <summary>Find the smallest prefix of source files in the original args that satisfy the 'f' predicate, or 'None'.</summary>
/// <remarks>
/// Can be helpful to eg. find the first file that causes non-deterministic compilation.
/// Only works for non-exe projects. <code cref="A"></code>
/// </remarks>
let binSearchProjectSourceList (args : SArgs) (f : SArgs -> bool) =
    // Create an array of args objects starting from one with shortest possible inputs list to full original args.
    let argsSet = getAllPlausibleArgsWithShortenedSourceListForLibrary args
    binSearchArrayIndices f argsSet

/// Compile a project and extract information out of it helpful in determinism investigations
let compileAndExtract
    (useTmpDir : bool)
    (name : string)
    (baseDir : string)
    (projectArgs : ProjectWithArgs)
    (fscDll : string)
    : Extract
    =
    let finalDir = Path.Combine(baseDir, name)
    let outputDir =
        if useTmpDir then
            Log.Information("compileAndExtract {finalDir}", finalDir)
            "testoutput"
        else
            finalDir
    let outputDir = Path.Combine(Environment.CurrentDirectory, outputDir)
    Directory.CreateDirectory(outputDir) |> ignore
    let subPath name = Path.Combine(outputDir, name)
    let {Project = project; Args = args} = projectArgs
    let workDir = Path.GetDirectoryName(project)

    let dllPath = subPath Paths.dll
    let args =
        args
        |> SArgs.setOutput (Path.GetRelativePath(workDir, subPath Paths.dll))
        |> SArgs.setKeyValue "--refout" (Path.GetRelativePath(workDir, subPath Paths.ref))
        |> SArgs.setKeyValue "--debug" "portable"
    
    let argsFile = subPath Paths.args
    args
    |> SArgs.toFile argsFile
    
    CliWrap.Cli
        .Wrap("dotnet")
        .WithWorkingDirectory(workDir)
        .WithArguments($"{fscDll} @{argsFile}")
        .ExecuteAssertSuccess()
    
    let mvid = MvidReader.getMvid dllPath
    let mvidPath = subPath Paths.mvid
    File.WriteAllText(mvidPath, mvid.ToString())
    
    let extract = getExtract project name outputDir
    let json = JsonConvert.SerializeObject(extract, Formatting.Indented)
    File.WriteAllText(subPath Paths.extract, json)
    
    if useTmpDir then
        if Directory.Exists finalDir then
            failwith $"Output directory {finalDir} exists."
            
        // Make sure parent directory exists
        Directory.CreateDirectory(finalDir) |> ignore
        Directory.Delete(finalDir)
        Directory.Move(outputDir, finalDir)
    
    extract
