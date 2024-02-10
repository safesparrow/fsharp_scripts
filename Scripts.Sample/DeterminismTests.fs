module Scripts.DeterminismTests

open System
open Scripts.Git
open Scripts.More
open System.IO
open Scripts.Sample
open ArgsFile
open Serilog
open DeterminismExtracts
open Scripts.Build

let config = { CheckoutsConfig.CacheDir = Path.Combine(Scripts.Utils.repoDir, ".cache") }

let TestFcsCompilationDeterminism (fscDll : string) =
    let spec = fantomas
    let dir = SamplePreparation.prepare config spec
    let project = Path.Combine(dir, "src", "Fantomas", "Fantomas.fsproj")
    
    let binlogPath = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    buildProject project (Some binlogPath) ""
    let args = generateCompilationArgs project []
    
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
