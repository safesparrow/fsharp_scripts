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

let rec buildProjectMinimal (projectPath : string) (binlogOutputPath : string) (extraArgs : string) =
    if File.Exists(projectPath) = false then
        failwith $"'{nameof buildProjectMinimal}' expects a project file path, but {projectPath} is not a file or it doesn't exist"
    let projectFile = Path.GetFileName(projectPath)
    Cli
        .Wrap(dotnetPath)
        .WithWorkingDirectory(Path.GetDirectoryName(projectPath))
        .WithArguments($"build --no-incremental --no-dependencies --no-restore {projectFile} -- /bl:{binlogOutputPath} {extraArgs}")
        .ExecuteAssertSuccess()

let run () =
    let config =
        {
            CheckoutsConfig.CacheDir = Path.Combine(repoDir, ".cache")
        }
        
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
        buildProjectMinimal projectPath binlog "/p:BUILDING_USING_DOTNET=true"
        let args = mkCompilerArgsFromBinLog binlog
        File.WriteAllText(respFile, args)
    Log.Information($"Compilation args stored in {respFile}")
    let awp = {ArgsFileWithProject.ArgsFile = respFile; ArgsFileWithProject.Project = projectPath}
    
    let compilation = makeCompilationCommand fscDll awp
    printfn $"hyperfine --shell PowerShell -L 'FSC_Profile' '1,0' 'cd {compilation.WorkingDirPath} ; $env:FSC_Profile={{FSC_Profile}} ; {compilation.TargetFilePath} {compilation.Arguments}' --runs 2 --show-output"
    ()
    