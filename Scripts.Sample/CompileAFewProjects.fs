module Scripts.Sample.CompileAFewProjects
open System
open System.IO
open CliWrap
open Scripts
open Scripts.Compiler
open Scripts.Build
open Scripts.More
open Scripts.Sample
open Scripts.Git
open Serilog
open Utils
open ArgsFile

let config =
    {
        CheckoutsConfig.CacheDir = Path.Combine(repoDir, ".cache")
    }

let run () =
        
    let compilerCheckout =
        SamplePreparation.prepare config fsharp_20240127
        |> CompilerCheckout
        
    let publishOptions = Some defaultPublishOptions
    let fscDll = publishCompiler compilerCheckout publishOptions
    let sample = fsharp_20240127
    let sampleDir = SamplePreparation.prepare config sample
    
    let projectPath = Path.Combine(sampleDir, Paths.fcs)
    let binlog = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    let respFile = Path.Combine(Environment.CurrentDirectory, "compile.rsp")
    
    if not (File.Exists respFile) then 
        buildProjectMinimal projectPath (Some binlog) "/p:BUILDING_USING_DOTNET=true"
        ()
        // let args = mkCompilerArgsFromBinLog None binlog
        // File.WriteAllText(respFile, args)
    Log.Information($"Compilation args stored in {respFile}")
    let awp = {ArgsFileWithProject.ArgsFile = respFile; ArgsFileWithProject.Project = projectPath}
    
    let compilation = makeCompilationCommand fscDll awp
    printfn $"hyperfine --shell PowerShell -L 'FSC_Profile' '1,0' 'cd {compilation.WorkingDirPath} ; $env:FSC_Profile={{FSC_Profile}} ; {compilation.TargetFilePath} {compilation.Arguments}' --runs 2 --show-output"
    ()
    