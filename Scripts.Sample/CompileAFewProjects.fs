module Scripts.Sample.CompileAFewProjects
open System
open System.IO
open CliWrap
open Scripts
open Scripts.Compiler
open Scripts.More
open Scripts.Sample
open Scripts.Git
open Serilog
open Utils
open ArgsFile

let buildProject (path : string) (binlog : string) (extraArgs : string) =
    Cli
        .Wrap(dotnetPath)
        .WithWorkingDirectory(Path.GetDirectoryName(path))
        .WithArguments($"build --no-incremental /bl:{binlog} {extraArgs}")
        .ExecuteAssertSuccess()

let run () =
    let config =
        {
            CheckoutsConfig.CacheDir = Path.Combine(Utils.repoDir, ".cache")
        }
        
    let fsharp =
        {
            Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("safesparrow", "fsharp", "948f8b2f9cea960c4286bf957ef0e3a1c591ed0f")
            PrepareScript = PrepareScript.JustBuild
        }
        
    let compilerCheckout =
        SamplePreparation.prepare config fsharp
        |> CompilerCheckout
        
    let publishOptions = Some defaultPublishOptions
        
    let fscDll = publishCompiler compilerCheckout publishOptions
    //
    // let sample = fantomas
    // let sampleDir = SamplePreparation.prepare config sample
    //
    // let fantomasCorePath = Path.Combine(sampleDir, "src", "Fantomas.Core", "Fantomas.Core.fsproj")
    // let binlog = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    // let respFile = Path.Combine(Environment.CurrentDirectory, "fantomas.core.rsp")
    
    
    let sample = {fsharp with CodebaseSpec = CodebaseSpec.MakeGithub("safesparrow", "fsharp", "948f8b2f9cea960c4286bf957ef0e3a1c591ed0f", "2") }
    let sampleDir = SamplePreparation.prepare config sample
    
    let projectPath = Path.Combine(sampleDir, "src", "compiler", "FSharp.Compiler.Service.fsproj")
    let binlog = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    let respFile = Path.Combine(Environment.CurrentDirectory, "compile.rsp")
    
    if not (File.Exists respFile) then 
        buildProject projectPath binlog "/p:BUILDING_USING_DOTNET=true"
        let args = mkCompilerArgsFromBinLog binlog
        File.WriteAllText(respFile, args)
    Log.Information($"Compilation args stored in {respFile}")
    let awp = {ArgsFileWithProject.ArgsFile = respFile; ArgsFileWithProject.Project = projectPath}
    
    let compilation = makeCompilationCommand fscDll awp
    printfn $"hyperfine --shell PowerShell -L 'FSC_Profile' '1,0' 'cd {compilation.WorkingDirPath} ; $env:FSC_Profile={{FSC_Profile}} ; {compilation.TargetFilePath} {compilation.Arguments}' --runs 2 --show-output"
    ()
    