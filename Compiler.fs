/// Helpers for operating on a local dotnet/fsharp compiler codebase
module Scripts.Compiler

open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open CliWrap
open Scripts.ArgsFile
open Serilog
open Utils

[<RequireQualifiedAccess>]
type OS =
    | Windows
    | Linux

let os =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        OS.Windows
    elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
        OS.Linux
    else
        failwith "Only Windows and Linux OS platforms are supported"        

type String with
    member this.Combine(subpath : string) : string = Path.Combine(this, subpath)

/// Path to a locally checked out repo
type CompilerCheckout =
    | CompilerCheckout of string
        member this.Path = match this with CompilerCheckout path -> path
        override this.ToString() = this.Path
        member this.Combine(subpath : string) = this.Path.Combine(subpath)

let fcsSubpath = Path.Combine("src", "compiler", "FSharp.Compiler.Service.fsproj")
let fscSubpath = Path.Combine("src", "fsc", "fscProject", "fsc.fsproj")

let isLikelyCompilerRepo (checkout : CompilerCheckout) : bool =
    File.Exists(Path.Combine(checkout.Path, fcsSubpath))
    
let assertIsCompilerRepo (checkout : CompilerCheckout) : unit =
    if not (isLikelyCompilerRepo checkout) then
        failwith $"'{checkout}' does not look like a valid compiler repo"

let clean (checkout : CompilerCheckout) =
    assertIsCompilerRepo checkout
    
    Cli
        .Wrap("git")
        .WithWorkingDirectory(checkout.Path)
        .WithArguments($"clean -xdf")
        .ExecuteAssertSuccess()

type RID = string
module RID =
    let WinX64 = "win-x64"
    let LinuxX64 = "linux-x64"

let autoRid =
    match os with
    | OS.Windows -> RID.WinX64
    | OS.Linux -> RID.LinuxX64

/// <summary>
/// These are the env variables that Ionide.ProjInfo seems to set (in-process) - see <a href="https://github.com/ionide/proj-info/blob/963dc87efd2035a6e250f2076878211b43140cbc/src/Ionide.ProjInfo/Library.fs#L251-L257">code</a>.
/// We need to get rid of them so that the child 'dotnet run' process is using the right tools
/// </summary>
let private projInfoEnvVariables =
    [
        "MSBuildExtensionsPath"
        "DOTNET_ROOT"
        "MSBUILD_EXE_PATH"
        "DOTNET_HOST_PATH"
        "MSBuildSDKsPath"
    ]

let private emptyProjInfoEnvironmentVariables =
    projInfoEnvVariables
    |> List.map (fun var -> var, "")
    |> readOnlyDict

type Configuration =
    | Debug
    | Release

[<RequireQualifiedAccess>]
type BuildType =
    | Arcade
    | Dotnet

type PublishOptions =
    {
        Configuration : Configuration
        Rid : RID option
        ReadyToRun : bool
        TargetFramework : string
        DotnetPath : string
        // BuildType : BuildType
    }

let dotnetPath =
    Environment.GetEnvironmentVariable("FSHARP_SCRIPTS_DOTNET")
    |> Option.ofObj
    |> Option.defaultValue "dotnet"
    
let defaultPublishOptions =
    {
        Configuration = Configuration.Release
        Rid = None
        ReadyToRun = true
        TargetFramework = "net7.0"
        DotnetPath = dotnetPath
        // BuildType = BuildType.Dotnet
    }

let compilerPublishSubPath (configuration : Configuration) (tfm : string) (rid : RID) : string =
    Path.Combine("artifacts", "bin", "fsc", configuration.ToString(), tfm, rid.ToString(), "publish", "fsc.dll")
    
let assertArcadeNotUsed (checkout : CompilerCheckout) =
    if File.Exists(checkout.Combine(".dotnet")) then
        failwith ($"Compiler checkout {checkout} has the '.dotnet' directory created which indicates it used Arcade build at some point." +
                  "This script only works with dotnet-based builds. Delete that directory to proceed.")
//
// /// Creates a command for building with the build script 
// let buildScriptCommand (checkout : CompilerCheckout) (c : Configuration) : Command =
//     let scriptName = match os with | OS.Windows -> "Build.cmd" | OS.Linux -> "build.sh"
//     let scriptPath = checkout.Combine(scriptName)
//     Cli
//         .Wrap(scriptPath)
//         .WithEnvironmentVariables(emptyProjInfoEnvironmentVariables)
//         .WithWorkingDirectory(checkout.Path)
//         .WithArguments($"-c {c} -noVisualStudio")        

let publishCompilerCommand (checkout : CompilerCheckout) (options : PublishOptions) : Command =
    let options = options
    let dotnetPath = options.DotnetPath
    
    let fscProj = checkout.Combine(fscSubpath)
    let dir = Path.GetDirectoryName(fscProj)

    let rid = options.Rid |> Option.defaultValue autoRid
    Cli
        .Wrap(dotnetPath)
        .WithEnvironmentVariables(emptyProjInfoEnvironmentVariables)
        .WithWorkingDirectory(dir)
        .WithArguments($"publish -c {options.Configuration} -r {rid} -p:PublishReadyToRun={options.ReadyToRun} -f {options.TargetFramework} " +
                       "--no-self-contained /p:BUILDING_USING_DOTNET=true /p:AppendRuntimeIdentifierToOutputPath=false")

/// <summary>
/// Builds the FSC compiler.
/// Depends on regular 'dotnet' and assumes Arcade build was not previously run.
/// </summary>
/// <returns>Path to resulting fsc.dll file.</returns>
/// <remarks></remarks>
let publishCompiler (checkout : CompilerCheckout) (options : PublishOptions option) : string =
    Log.Information($"Building compiler in '{checkout}'")
    let options = options |> Option.defaultValue defaultPublishOptions
    assertArcadeNotUsed checkout

    let command = publishCompilerCommand checkout options
    command.ExecuteAssertSuccess()

    let rid = options.Rid |> Option.defaultValue autoRid
        
    let fscDll = checkout.Combine(compilerPublishSubPath options.Configuration options.TargetFramework rid)
    let f = FileInfo(fscDll)
    if not f.Exists then
        failwith $"Fsc dll '{fscDll}' does not exist after publish"
    // if (f.LastWriteTime < DateTime.Now.Subtract(TimeSpan.FromMinutes(2))) then
    //     failwith $"Expected fsc dll '{fscDll}' to be recently written (in the last two minutes), but it was not - it's likely an issue with the build process"
    
    Log.Information($"Compiler published: '{fscDll}'")
    
    fscDll

let propsForCustomCompilerInBuild (fscDll : string) =
    [
        "DisableAutoSetFscCompilerPath", "false"
        "DotnetFscCompilerPath", fscDll
        "FSharpPreferNetFrameworkTools", "false"
        "FSharpPrefer64BitTools", "true"        
    ]
    |> Map.ofList

let propsToMSBuildArgs (props : Map<string, string>) : string =
    props
    |> Map.toList
    |> List.map (fun (name, value) -> $"/p:{name}={value}")
    |> fun x -> String.Join(" ", x)
    
let argsUsingResponseFile (file : string) =
    $"@{file}"
    
let makeCompilationCommand (fscDll : string) (argsWithProject : ArgsFileWithProject) =
    let args = $"{fscDll} {argsUsingResponseFile argsWithProject.ArgsFile}"
    Log.Information($"Compiling '{argsWithProject.Project}' using '{fscDll}'")
    Cli
        .Wrap(dotnetPath)
        .WithWorkingDirectory(Path.GetDirectoryName(argsWithProject.Project))
        .WithArguments(args)