[<NUnit.Framework.TestFixture>]
module Scripts.DeterminismTests

open Scripts.More

open System.IO
open Ionide.ProjInfo
open NUnit.Framework
open Scripts.Git
open Scripts.Sample
open ArgsFile
open Serilog
open DeterminismExtracts

open Scripts.Test

// [<Test>]
let testMkArgsDeterminism () =
    let sample = determinism
    let projRelativePath = "DeterminismSample.fsproj"
    let baseDir = SamplePreparation.prepare config sample
    let _projFile = Path.Combine(baseDir, projRelativePath)
    ()
    //generateCompilationArgs projFile (Path.Combine(__SOURCE_DIRECTORY__, "determinism_projinfo.args"))
    
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