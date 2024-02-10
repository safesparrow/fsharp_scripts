module Scripts.IDE

open System.IO
open FSharp.Compiler.CodeAnalysis
open Ionide.ProjInfo.Types
open Scripts.Compiler
open System
open Ionide.ProjInfo
open Serilog

type Project =
    { Raw: ProjectOptions
      FCS: FSharpProjectOptions }

    member this.Name = this.Raw.ProjectFileName
    member this.ShortName = Path.GetFileNameWithoutExtension(this.Name)

type FSharpProjectOptions with

    member x.ProjectDir = Path.GetDirectoryName(x.ProjectFileName)

let pathRelativeToProject (op: FSharpProjectOptions) (filename: string) =
    if Object.ReferenceEquals(null, op) then
        filename
    else
        Path.GetRelativePath(op.ProjectDir, filename)

let private subscribeToChecker (checker: FSharpChecker) =
    checker.FileChecked.AddHandler(fun (sender: obj) (filename: string, op: FSharpProjectOptions) ->
        let name =
            (if Object.ReferenceEquals(null, op) then
                 "-"
             else
                 op.ProjectFileName)
            |> Path.GetFileNameWithoutExtension

        Log.Information("{project} | FileChecked {file}", name.PadRight(20), pathRelativeToProject op filename)
        ())

    checker.FileParsed.AddHandler(fun (sender: obj) (filename: string, op: FSharpProjectOptions) ->
        let name =
            (if Object.ReferenceEquals(null, op) then
                 "-"
             else
                 op.ProjectFileName)
            |> Path.GetFileNameWithoutExtension

        Log.Information("{project} | FileParsed  {file}", name.PadRight(20), pathRelativeToProject op filename)
        ())

    checker.ProjectChecked.AddHandler(fun (sender: obj) (op: FSharpProjectOptions) ->
        let name =
            (if Object.ReferenceEquals(null, op) then
                 "-"
             else
                 op.ProjectFileName)
            |> Path.GetFileNameWithoutExtension

        Log.Information("{project} | ProjectChecked", name.PadRight(20))
        ())

type CheckerOptions =
    { UseTransparentCompiler: bool
      EnablePartialTypeChecking: bool
      ParallelReferenceResolution: bool }

type IDE(slnPath: string, ?configuration: Configuration, ?checkerOptionsOverrides: CheckerOptions -> CheckerOptions) =
    let configuration = configuration |> Option.defaultValue Configuration.Debug
    let slnDir = DirectoryInfo(Path.GetDirectoryName(slnPath))
    let toolsPath = Init.init slnDir None
    let globalProps = [ "Configuration", configuration.ToString() ]
    let workspaceLoader = WorkspaceLoaderViaProjectGraph.Create(toolsPath, globalProps)
    let mutable projects: Map<string, Project> = Map.empty

    let defaultCheckerOptions =
        { ParallelReferenceResolution = true
          EnablePartialTypeChecking = true
          UseTransparentCompiler = true }

    let checkerOptions =
        checkerOptionsOverrides
        |> Option.map (fun overrides -> overrides defaultCheckerOptions)
        |> Option.defaultValue defaultCheckerOptions

    let checker =
        FSharpChecker.Create(
            parallelReferenceResolution = checkerOptions.ParallelReferenceResolution,
            enablePartialTypeChecking = checkerOptions.EnablePartialTypeChecking,
            useTransparentCompiler = checkerOptions.UseTransparentCompiler
        )

    do subscribeToChecker checker

    member x.RestoreSln() = Scripts.Build.restoreProject slnPath ""

    member x.BuildSln() =
        Scripts.Build.buildProject slnPath None ""

    member x.LoadProjects() =
        let ps = workspaceLoader.LoadSln(slnPath) |> Seq.toArray
        let fcsProjects = FCS.mapManyOptions ps |> Seq.toArray

        projects <-
            Array.zip ps fcsProjects
            |> Array.map (fun (raw, fcs) -> raw.ProjectFileName, { Project.Raw = raw; Project.FCS = fcs })
            |> Map.ofArray

    member x.Projects = projects

    member x.CheckAllProjectsInParallel() =
        projects
        |> Seq.map (fun (KeyValue(n, p)) ->
            async {
                let res = checker.ParseAndCheckProject(p.FCS) |> Async.StartAsTask |> _.Result
                return res
            })
        |> Seq.toArray
        |> Async.Parallel
        |> Async.StartAsTask
        |> fun t ->
            for projRes in t.Result do
                for d in projRes.Diagnostics do
                    Log.Information(
                        "Diagnostic | {project} | {filename} | {message}",
                        Path.GetFileName(projRes.ProjectContext.ProjectOptions.ProjectFileName),
                        d.FileName,
                        d.Message
                    )