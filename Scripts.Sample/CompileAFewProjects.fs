module Scripts.Sample.CompileAFewProjects
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

let buildProject (path : string) (binlog : string) =
    Cli
        .Wrap(dotnetPath)
        .WithWorkingDirectory(Path.GetDirectoryName(path))
        .WithArguments($"build --no-incremental /bl:{binlog}")
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
    
    let sample = fantomas
    let sampleDir = SamplePreparation.prepare config sample
    
    let fantomasCorePath = Path.Combine(sampleDir, "src", "Fantomas.Core", "Fantomas.Core.fsproj")
    let binlog = "x.binlog"
    buildProject fantomasCorePath binlog
    let args = ArgsFile.mkCompilerArgsFromBinLog binlog
    let respFile = "fantomas.core.rsp"
    File.WriteAllText(respFile, args)
    Log.Information($"Compilation args stored in {respFile}")
    let awp = {ArgsFileWithProject.ArgsFile = respFile; ArgsFileWithProject.Project = fantomasCorePath}
    runCompilation fscDll awp 
    ()
    