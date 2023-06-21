/// Helpers for operating on a local dotnet/fsharp compiler codebase
module Scripts.Compiler

open System
open System.IO
open System.Runtime.InteropServices
open Serilog
open Utils

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
    
    CliWrap.Cli
        .Wrap("git")
        .WithWorkingDirectory(checkout.Path)
        .WithArguments($"clean -xdf")
        .ExecuteAssertSuccess()

type RID =
    | WinX64
    | LinuxX64

let autoRid =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        RID.WinX64
    else
        RID.LinuxX64

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

type PublishOptions =
    {
        Configuration : Configuration
        Rid : RID option
        ReadyToRun : bool
        TargetFramework : string
        DotnetPath : string
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
    }

let compilerPublishSubPath (configuration : Configuration) (rid : RID) : string =
    Path.Combine("artifacts", "bin", "fsc", configuration.ToString(), rid.ToString(), "publish", "fsc.dll")
    
let assertArcadeNotUsed (checkout : CompilerCheckout) =
    if File.Exists(checkout.Combine(".dotnet")) then
        failwith ($"Compiler checkout {checkout} has the '.dotnet' directory created which indicates it used Arcade build at some point." +
                  "This script only works with dotnet-based builds. Delete that directory to proceed.")
    
/// <summary>
/// Builds the FSC compiler.
/// Depends on regular 'dotnet' and assumes Arcade build was not previously run.
/// </summary>
/// <returns>Path to resulting fsc.dll file.</returns>
let publishCompiler (checkout : CompilerCheckout) (options : PublishOptions option) : string =
    Log.Information($"Building compiler in '{checkout}'")
    
    assertArcadeNotUsed checkout
    
    let options = options |> Option.defaultValue defaultPublishOptions
    let fscProj = checkout.Combine(fscSubpath)
    let dir = Path.GetDirectoryName(fscProj)
    let rid = options.Rid |> Option.defaultValue autoRid
    
    CliWrap.Cli
        .Wrap(options.DotnetPath)
        .WithEnvironmentVariables(emptyProjInfoEnvironmentVariables)
        .WithWorkingDirectory(dir)
        .WithArguments($"publish -c {options.Configuration} -r {options.Rid} -p:PublishReadyToRun={options.ReadyToRun} -f {options.TargetFramework} " +
                       "--no-self-contained /p:BUILDING_USING_DOTNET=true /p:AppendRuntimeIdentifierToOutputPath=false")
        .ExecuteAssertSuccess()
        
    let fscDll = checkout.Combine(compilerPublishSubPath options.Configuration rid)
    let f = FileInfo(fscDll)
    if not (f.Exists) then
        failwith $"Fsc dll '{fscDll}' does not exist after publish"
    if (f.LastWriteTime < DateTime.Now.Subtract(TimeSpan.FromMinutes(2))) then
        failwith $"Expected fsc dll '{fscDll}' to be recently written (in the last two minutes), but it was not - it's likely an issue with the build process"
    
    Path.Combine()