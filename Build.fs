module Scripts.Build

open System
open System.IO
open CliWrap
open Scripts
open Serilog
open Utils

let dotnetPath =
    Environment.GetEnvironmentVariable("FSHARP_SCRIPTS_DOTNET")
    |> Option.ofObj
    |> Option.defaultValue "dotnet"

let rec restoreProject (projectPath : string) (extraArgs : string) =
    if File.Exists(projectPath) = false then
        failwith $"'{nameof restoreProject}' expects a project file path, but {projectPath} is not a file or it doesn't exist"
    let projectFile = Path.GetFileName(projectPath)
    Log.Information("Start buildProject {projectPath}", projectPath)
    Cli
        .Wrap(dotnetPath)
        .WithWorkingDirectory(Path.GetDirectoryName(projectPath))
        .WithArguments($"restore {projectFile} {extraArgs}")
        .ExecuteAssertSuccess()
    Log.Information("End restoreProject {projectPath}", projectPath)

let rec buildProject (projectPath : string) (binlogOutputPath : string option) (extraArgs : string) =
    if File.Exists(projectPath) = false then
        failwith $"'{nameof buildProject}' expects a project file path, but {projectPath} is not a file or it doesn't exist"
    let projectFile = Path.GetFileName(projectPath)
    Log.Information("Start buildProject {projectPath}", projectPath)
    let binlogArg = match binlogOutputPath with Some binlogOutputPath -> $"/bl:{binlogOutputPath}" | None -> ""
    Cli
        .Wrap(dotnetPath)
        .WithWorkingDirectory(Path.GetDirectoryName(projectPath))
        .WithArguments($"build {projectFile} {binlogArg} {extraArgs}")
        .ExecuteAssertSuccess()
    Log.Information("End buildProject {projectPath}", projectPath)

let rec buildSingleProjectMinimalNoIncremental (projectPath : string) (binlogOutputPath : string option) (extraArgs : string) =
    let extraArgs = extraArgs + " --no-incremental --no-dependencies --no-restore"
    buildProject projectPath binlogOutputPath extraArgs
