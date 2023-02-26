[<NUnit.Framework.TestFixture>]
module Scripts.Test

open System
open System.IO
open System.Security.Cryptography
open NUnit.Framework
open Newtonsoft.Json
open Scripts.Git
open Scripts.Sample
open ArgsFile    

let config =
    {
        CheckoutsConfig.CacheDir =  "c:/projekty/fsharp/fsharp_scripts/.cache"
    }

type AllConfig =
    {
        Dir : string
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

type ProjectSArgs =
    {
        Project : string // fsproj path
        Args : SArgs
    }

[<AutoOpen>]
module Codebases =
    let fantomas =
        {
            Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "fantomas", "18f31541e983c9301e6a55ba6582817bc704cb6f")
            PrepareScript = PrepareScript.JustBuild
        }
    
    let fsharp =
        {
            Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("dotnet", "fsharp", "409168556aed9c6ec8595d1d9525fb5b88888fc4")
            PrepareScript = PrepareScript.PowerShell "./Build.cmd -noVisualStudio"
        }

// [<Test>]
let TestCheckouts () =
    let sample = fsharp
    let projRelativePath = "src/Compiler/FSharp.Compiler.Service.fsproj"
    // SamplePreparation.prepare config sample
    let baseDir = SamplePreparation.codebaseDir config sample.CodebaseSpec
    let projFile = Path.Combine(baseDir, projRelativePath)
    mkArgsFile projFile (Path.Combine(__SOURCE_DIRECTORY__, "fsc_new.args"))

open Newtonsoft.Json.Linq

let getReportWarningsTyparFromJson (path : string) =
    let json = File.ReadAllText(path)
    let o = JObject.Parse(json)
    let x = o.SelectToken("$..children[?(@.name == 'ReportWarnings')].children[?(@.kind == 'Typar')].name")
    if x = null then None
    else x.Value<string>() |> Some


module Extracting =

    [<CLIMutable>]
    type ExtractCore =
        {
            Mvid : string
            DllHash : string
            PdbHash : string
            RefHash : string
            Project : string
            SigDataHash : string
        }

    [<CLIMutable>]
    type ExtractMeta =
        {
            DllTimestamp : DateTime
            Name : string
            Directory : string
        }

    [<CLIMutable>]
    type Extract =
        {
            Core : ExtractCore
            Meta : ExtractMeta
        }

    module Paths =
        let mvid = "mvid.txt"
        let dll = "out.dll"
        let pdb = "out.pdb"
        let ref = "ref.dll"
        let args = "fscargs.txt"
        let extract = "extract.json"
        let sigData : string = "out.signature-data.json"

    let getFileHash (file : string) =
        if not (File.Exists(file)) then
            failwith $"File '{file}' does not exist"
        use md5 = MD5.Create()
        use stream = File.OpenRead(file)
        let hash = md5.ComputeHash(stream)
        BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()

    let getExtract (project : string) (name : string) (dir : string) =
        let subPath name = Path.Combine(dir, name)
        let dll = subPath Paths.dll
        {
            Extract.Core =
                {
                    ExtractCore.Mvid = File.ReadAllText(subPath Paths.mvid).Trim()
                    DllHash = getFileHash dll
                    PdbHash = getFileHash (subPath Paths.pdb)
                    RefHash = getFileHash (subPath Paths.ref)
                    Project = project
                    SigDataHash = getFileHash (subPath Paths.sigData)
                }
            Extract.Meta =
                {
                    ExtractMeta.Directory = dir
                    DllTimestamp = File.GetLastWriteTimeUtc(dll)
                    Name = name
                }
        }

open Extracting

let go (name : string) (outputDir : string) (projectArgs : ProjectSArgs) (fscDll : string) (i : int) =
    printfn $"go {name} {outputDir}"
    let finalDir = outputDir
    let tmp = $"testoutput_{i}"
    let outputDir = tmp
    Directory.CreateDirectory(outputDir) |> ignore
    let subPath name = Path.Combine(Environment.CurrentDirectory, Path.Combine(outputDir, name))
    let dllPath = subPath Paths.dll
    let {Project = project; Args = args} = projectArgs
    let args =
        args
        |> SArgs.setOutput dllPath
        |> SArgs.setKeyValue "--refout" (subPath Paths.ref)
        |> SArgs.setKeyValue "--debug" "portable"
    
    let argsFile = subPath Paths.args
    args
    |> SArgs.toFile argsFile
    
    CliWrap.Cli
        .Wrap("dotnet")
        .WithWorkingDirectory(Path.GetDirectoryName(project))
        .WithArguments($"{fscDll} @{argsFile}")
        .ExecuteAssertSuccess()
    
    let mvid = MvidReader.getMvid dllPath
    let mvidPath = subPath Paths.mvid
    File.WriteAllText(mvidPath, mvid.ToString())
    
    let extract = getExtract project name outputDir
    let json = JsonConvert.SerializeObject(extract, Formatting.Indented)
    File.WriteAllText(subPath Paths.extract, json)
    if Directory.Exists finalDir then Directory.Delete(finalDir, true)
    Directory.CreateDirectory(finalDir) |> ignore
    Directory.Delete(finalDir)
    Directory.Move(outputDir, finalDir)
    extract

/// Find the highest number x in [a, b] for which `f x = true` 
let rec binSearch (a : int) (b : int) (f : int -> bool) =
    printfn $"binSearch {a} {b}"
    if a = b then (if f a then a else a-1) else
    if a >= b then a
    else
        let m = (a + b) / 2
        let res = f m
        if res then binSearch (m+1) b f
        else binSearch a m f

[<Test>]
let TestFindNondeterministicFile () =
    
    // SamplePreparation.prepare config fsharp
    // printfn "Prepared"
    let dir = SamplePreparation.codebaseDir config fsharp.CodebaseSpec
    // We have FSC args that we know produce non-deterministic results.
    let path = Path.Combine(__SOURCE_DIRECTORY__, "fsc_new.args")
    let args =
        SArgs.ofFile path
        |> SArgs.setTestFlag "GraphBasedChecking" true
        // |> SArgs.setBool "deterministic" true
        |> SArgs.setTestFlag "DumpSignatureData" true
    
    let fsIndices = 
        args.Inputs
        |> List.indexed
        |> List.filter (fun (_, i) -> i.EndsWith(".fs"))
        |> List.toArray
    
    // printfn $"FS indices to bin search: %+A{fsIndices}"
    let project = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\src\compiler\FSharp.Compiler.Service.fsproj"
    let fsc = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"

    let f (i : int) =
        let idx, file = fsIndices[i]
        printfn $"[{i}] Compiling up to file index {idx}, file {file}"
        let args = args |> SArgs.limitInputsCount (idx+1)

        let projectArgs =
            {
                Project = project
                Args = args
            }
        let dir = "test_output"
        
        let extracts =
            [|0..0|]
            |> Array.chunkBySize 4
            |> Array.collect (fun items ->
                items
                |> Array.Parallel.map (fun i ->
                    let outDir = $"idx_{idx}/fsc_{i}"
                    let e = go $"fsc_{i}" outDir projectArgs fsc i
                    let sigPath = Path.Combine(outDir, Paths.sigData)
                    let typar = getReportWarningsTyparFromJson sigPath
                    printfn $"{outDir} [{i}] - typar={typar}"
                    typar
                )
            )
            
        let distincts =
            extracts
            |> Array.distinctBy (fun e -> e)
        printfn $"%+A{distincts}"
        
        match distincts with
        | [|single|] -> printfn $"[{idx} files] Single typar {single}"; single <> Some "b"
        | multiple -> printfn $"[{idx} files] {multiple.Length} distinct typars found: {multiple}"; false
    
    let trySingle () =
        let doIdx =
            fsIndices
            |> Array.indexed
            |> Array.find (fun (idx, (i, f)) -> f.EndsWith("ConstraintSolver.fs"))
            |> snd |> fst
        let args = args |> SArgs.limitInputsCount (doIdx+1)
        let projectArgs =
            {
                Project = project
                Args = args
            }
        let idx = doIdx
        let i = 0
        let outDir = $"idx_{idx}/fsc_{i}"
        let e = go $"fsc_{i}" outDir projectArgs fsc i
        let sigPath = Path.Combine(outDir, Paths.sigData)
        let typar = getReportWarningsTyparFromJson sigPath
        printfn $"{outDir} [{i}] - typar={typar}"
    
    trySingle ()
    0

    //
    // let doIdx =
    //     fsIndices
    //     |> Array.indexed
    //     |> Array.find (fun (idx, (i, f)) -> f.EndsWith("DiagnosticOptions.fs"))
    //     |> fst
    // binSearch doIdx (fsIndices.Length-1) f

    // let searched = binSearch 0 (fsIndices.Length-1) f
    // File.WriteAllText("c:/jan/fsharp_scripts/binsearch.idx", searched.ToString())
    // printfn $"Found last deterministic index to be: {searched}"