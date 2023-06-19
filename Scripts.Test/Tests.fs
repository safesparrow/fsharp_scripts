[<NUnit.Framework.TestFixture>]
module Scripts.Test

open System
open System.IO
open Ionide.ProjInfo
open Ionide.ProjInfo.Types
open NUnit.Framework
open Newtonsoft.Json
open Scripts.Git
open Scripts.Sample
open ArgsFile
open Serilog

let config =
    {
        CheckoutsConfig.CacheDir =  "c:/projekty/fsharp/fsharp_scripts/.cache"
    }

// [<Test>]
let TestArgs () =
    let argsString = File.ReadAllText (Path.Combine(__SOURCE_DIRECTORY__, "fsc_new.args"))
    let rawArgs = FscArgs.split argsString
    let args = FscArgs.parse rawArgs
    printfn $"{args.Length} args parsed:"
    // printfn $"%+A{args}"
    
    let stringified = FscArgs.stringifyAll args
    Assert.That(stringified, Is.EquivalentTo rawArgs)
    
    let structured = args |> SArgs.structurize
    printfn $"{structured}"
    let destructured = structured |> SArgs.destructurize
    printfn $"{destructured}"
    Assert.That(destructured, Is.EquivalentTo args)  
    
    let modified = structured |> SArgs.clearTestFlag "ParallelCheckingWithSignatureFilesOn"
    printfn $"{modified}"
    Assert.That(modified |> SArgs.destructurize |> Array.length, Is.EqualTo (destructured.Length - 1))

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

// [<Test>]
let testMkArgsDeterminism () =
    let sample = determinism
    let projRelativePath = "DeterminismSample.fsproj"
    SamplePreparation.prepare config sample
    let baseDir = SamplePreparation.codebaseDir config sample.CodebaseSpec
    let projFile = Path.Combine(baseDir, projRelativePath)
    mkArgsFileProjInfo projFile (Path.Combine(__SOURCE_DIRECTORY__, "determinism_projinfo.args"))
    
let TestCheckoutsFantomas () =
    let sample = fantomas
    SamplePreparation.prepare config sample

open Newtonsoft.Json.Linq

let getJsonTokenFromFile (path : string) (jpath : string) =
    let json = File.ReadAllText(path)
    let o = JObject.Parse(json)
    let x = o.SelectToken(jpath)
    if x = null then None
    else x.Value<string>() |> Some

let reportWarningsTyparJPath = "$..children[?(@.name == 'ReportWarnings')].children[?(@.kind == 'Typar')].name"

open DeterminismExtracts

/// Compile a project and extract information out of it helpful in determinism investigations
let compileAndExtract (useTmpDir : bool) (name : string) (baseDir : string) (projectArgs : ProjectWithArgs) (fscDll : string) =
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

/// Find the lowest number x in [a, b] for which `f x = true`, or None if it doesn't exist 
let rec binSearch (a : int) (b : int) (f : int -> bool) =
    Log.Information("binSearch {range}", $"[{a}, {b}]")
    let f (x : int) =
        Log.Information("binSearch evaluation for {idx}", x)
        f x 
    if a = b then (if f a then Some a else None) else
    if a >= b then Some a
    else
        let m = (a + b + 1) / 2
        let res = f m
        if res then binSearch m b f
        else binSearch a (m-1) f

let TestFcsCompilationDeterminism (fscDll : string) =
    let path = Path.Combine(__SOURCE_DIRECTORY__, "determinism.args")
    let args =
        SArgs.ofFile path
        |> SArgs.setTestFlag "GraphBasedChecking" true
        // This assumes that graph-based TC is not auto-disabled in deterministic builds - requires a modified version of the compiler.
        |> SArgs.setBool "deterministic" true
        |> SArgs.setTestFlag "DumpSignatureData" true

    // let dir = SamplePreparation.codebaseDir config fsharp.CodebaseSpec
    // let project = Path.Combine(dir, "src/compiler/FSharp.Compiler.Service.fsproj")
    let dir = SamplePreparation.codebaseDir config determinism.CodebaseSpec
    let project = Path.Combine(dir, "DeterminismSample.fsproj")
    let projectArgs =
        {
            Project = project
            Args = args
        }
    
    let runs = 2
    let baseDir = "fcs_determinism_new"
    Log.Information("Running {runs} compilations sequentially in {baseDir}", runs, baseDir)
    let extracts =
        [|0..runs-1|]
        |> Array.map(fun i ->
            let outDir = $"{i}"
            let extract = compileAndExtract false $"determinism_{i}" baseDir projectArgs fscDll
            let sigPath = Path.Combine(outDir, Paths.sigData)
            // let t = getJsonTokenFromFile sigPath reportWarningsTyparJPath
            // Log.Information("[{outDir}] ReportWarnings Typar name = {name}", outDir, t)
            extract
        )
        
    let distincts =
        extracts
        |> Array.distinctBy (fun e -> e.Core)
    Log.Information("{runs} runs resulted in {distinctsLength} distinct extracts: {distincts}", runs, distincts.Length, distincts)

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

let getAllPlausibleArgsWithShortenedSourceList (args : SArgs) =
    match getOutputType args with
    | Exe -> failwith "Exe projects cannot have their source list shortened because the last source file is required as it represents the entry point."
    | _ ->
        let indices = getPlausibleSourceListPrefixEndIndices args.Inputs
        indices
        |> Array.map (fun lastSourceIdx ->
            args
            |> SArgs.limitInputsCount (lastSourceIdx+1)
        )

/// Find the smallest prefix of source files in the original args that satisfy the 'f' predicate.
/// Can be helpful to eg. find the first file that causes non-deterministic compilation.
/// Only works for non-exe projects.
let binSearchProjectSourceList (args : SArgs) (f : SArgs -> bool) =
    // Create an array of args objects starting from one with shortest possible inputs list to full original args.
    let argsSet = getAllPlausibleArgsWithShortenedSourceList args
    // Binary search that array 
    let f (argsSetIdx : int) =
        let args = argsSet[argsSetIdx]
        f args
    let prefixIdx = binSearch 0 argsSet.Length f
    prefixIdx
    |> Option.map (fun idx -> argsSet[idx])
//
// let TestFindNondeterministicFile () =
//     
//     // SamplePreparation.prepare config fsharp
//     // printfn "Prepared"
//     let dir = SamplePreparation.codebaseDir config fsharp.CodebaseSpec
//     // We have FSC args that we know produce non-deterministic results.
//     let path = Path.Combine(__SOURCE_DIRECTORY__, "fsc_new.args")
//     let args =
//         SArgs.ofFile path
//         //|> SArgs.setTestFlag "GraphBasedChecking" true
//         |> SArgs.setBool "deterministic" true
//         //|> SArgs.setTestFlag "DumpSignatureData" true
//     
//     let fsIndices = 
//         args.Inputs
//         |> List.indexed
//         |> List.filter (fun (_, i) -> i.EndsWith(".fs"))
//         |> List.toArray
//     
//     // printfn $"FS indices to bin search: %+A{fsIndices}"
//     let project = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\src\compiler\FSharp.Compiler.Service.fsproj"
//     let fsc = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
//
//     let f (i : int) =
//         let idx, file = fsIndices[i]
//         printfn $"[{i}] Compiling up to file index {idx}, file {file}"
//         let args = args |> SArgs.limitInputsCount (idx+1)
//
//         let projectArgs =
//             {
//                 Project = project
//                 Args = args
//             }
//         let dir = "test_output"
//         
//         let extracts =
//             [|0..0|]
//             |> Array.chunkBySize 4
//             |> Array.collect (fun items ->
//                 items
//                 |> Array.Parallel.map (fun i ->
//                     let outDir = $"idx_{idx}/fsc_{i}"
//                     let e = compileAndExtract $"fsc_{i}" outDir projectArgs fsc
//                     let sigPath = Path.Combine(outDir, Paths.sigData)
//                     let typar = getReportWarningsTyparFromJson sigPath
//                     printfn $"{outDir} [{i}] - typar={typar}"
//                     typar
//                 )
//             )
//             
//         let distincts =
//             extracts
//             |> Array.distinctBy (fun e -> e)
//         printfn $"%+A{distincts}"
//         
//         match distincts with
//         | [|single|] -> printfn $"[{idx} files] Single typar {single}"; single <> Some "b"
//         | multiple -> printfn $"[{idx} files] {multiple.Length} distinct typars found: {multiple}"; false
//     
//     let trySingle () =
//         let doIdx =
//             fsIndices
//             |> Array.indexed
//             |> Array.find (fun (idx, (i, f)) -> f.EndsWith("ConstraintSolver.fs"))
//             |> snd |> fst
//         let args = args |> SArgs.limitInputsCount (doIdx+1)
//         let projectArgs =
//             {
//                 Project = project
//                 Args = args
//             }
//         let idx = doIdx
//         let i = 0
//         let outDir = $"idx_{idx}/fsc_{i}"
//         let e = compileAndExtract $"fsc_{i}" outDir projectArgs fsc
//         let sigPath = Path.Combine(outDir, Paths.sigData)
//         let typar = getReportWarningsTyparFromJson sigPath
//         printfn $"{outDir} [{i}] - typar={typar}"
//     
//     trySingle ()
//     0
//
//     //
//     // let doIdx =
//     //     fsIndices
//     //     |> Array.indexed
//     //     |> Array.find (fun (idx, (i, f)) -> f.EndsWith("DiagnosticOptions.fs"))
//     //     |> fst
//     // binSearch doIdx (fsIndices.Length-1) f
//
//     // let searched = binSearch 0 (fsIndices.Length-1) f
//     // File.WriteAllText("c:/jan/fsharp_scripts/binsearch.idx", searched.ToString())
//     // printfn $"Found last deterministic index to be: {searched}"