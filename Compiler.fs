/// Helpers for operating on a local dotnet/fsharp compiler codebase
module Scripts.Compiler

open System.IO
open System.Runtime.InteropServices
open LibGit2Sharp
open Serilog
open Utils

/// Path to a locally checked out repo
type CompilerCheckout = string

let fcsSubpath = Path.Combine("src", "compiler", "FSharp.Compiler.Service.fsproj")
let fscSubpath = Path.Combine("src", "compiler", "fsc", "fsc.fsproj")

let isLikelyCompilerRepo (checkout : CompilerCheckout) : bool =
    File.Exists(Path.Combine(checkout, fcsSubpath))
    
let assertIsCompilerRepo (checkout : CompilerCheckout) : unit =
    if not (isLikelyCompilerRepo checkout) then
        failwith $"'{checkout}' does not look like a valid compiler repo"

let clean (checkout : CompilerCheckout) =
    assertIsCompilerRepo checkout
    
    CliWrap.Cli
        .Wrap("git")
        .WithWorkingDirectory(checkout)
        .WithArguments($"clean -xdf")
        .ExecuteAssertSuccess()

let autoRid =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        "win-x64"
    else
        "linux-x64"

let build (checkout : CompilerCheckout) =
    let fscProj = Path.Combine(checkout, fscSubpath)
    let dir = Path.GetDirectoryName(fscProj)
    let rid = autoRid
    let readyToRun = true
    let targetFramework = "net7.0"
    
    Log.Information($"Building compiler in '{checkout}'")
    
    CliWrap.Cli
        .Wrap("dotnet")
        .WithWorkingDirectory(dir)
        .WithArguments($"publish -c Release -r {rid} -p:PublishReadyToRun={readyToRun} -f {targetFramework} --no-self-contained")
        .ExecuteAssertSuccess()