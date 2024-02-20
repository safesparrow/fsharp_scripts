module Scripts.IDE

open System.IO
open System.Text.RegularExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open Ionide.ProjInfo.Types
open Scripts.Compiler
open System
open Ionide.ProjInfo
open Serilog
open Serilog.Events

type ProjectLanguage =
    | FSharp
    | CSharp

let projectLanguage (projectFile : string) =
    let extension = Path.GetExtension(projectFile)
    match extension with
    | ".fsproj" -> ProjectLanguage.FSharp
    | ".csproj" -> ProjectLanguage.CSharp
    | s -> failwith $"Unrecognised project extension '{extension}'"

type Project =
    { Raw: ProjectOptions
      FCS: FSharpProjectOptions option }

    member this.Name = this.Raw.ProjectFileName
    member this.ShortName = Path.GetFileNameWithoutExtension(this.Name)
    member this.Language = projectLanguage this.Raw.ProjectFileName

type FSharpProjectOptions with

    member x.ProjectDir = Path.GetDirectoryName(x.ProjectFileName)
    member x.HasNoCSharpReferences =
        x.ReferencedProjects
        |> Array.forall (function | FSharpReferencedProject.FSharpReference _ -> true | _ -> false)

let pathRelativeToProjectOrSolution (op: FSharpProjectOptions) (filename: string) (solutionPath : string) =
    if Object.ReferenceEquals(null, op) then
        Path.GetRelativePath(solutionPath, filename)
    else
        Path.GetRelativePath(op.ProjectDir, filename)

let private subscribeToChecker (solutionPath : string) (checker: FSharpChecker) =
    checker.FileChecked.AddHandler(fun (sender: obj) (filename: string, op: FSharpProjectOptions) ->
        let name =
            (if Object.ReferenceEquals(null, op) then
                 "-"
             else
                 op.ProjectFileName)
            |> Path.GetFileNameWithoutExtension

        Log.Information("{project} | FileChecked {file}", name.PadRight(20), pathRelativeToProjectOrSolution op filename solutionPath)
        ())

    checker.FileParsed.AddHandler(fun (sender: obj) (filename: string, op: FSharpProjectOptions) ->
        let name =
            (if Object.ReferenceEquals(null, op) then
                 "-"
             else
                 op.ProjectFileName)
            |> Path.GetFileNameWithoutExtension

        Log.Information("{project} | FileParsed  {file}", name.PadRight(20), pathRelativeToProjectOrSolution op filename solutionPath)
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

type IDE(slnPath: string, projectFilter: string option, ?configuration: Configuration,
         ?checkerOptionsOverrides: CheckerOptions -> CheckerOptions,
         ?msbuildProps: Map<string, string>) =
    let configuration = configuration |> Option.defaultValue Configuration.Debug
    let slnDir = DirectoryInfo(Path.GetDirectoryName(slnPath))
    let toolsPath = Init.init slnDir None
    
    let msbuildPropsStringCommandLineString =
        msbuildProps
        |> Option.defaultValue Map.empty
        |> Seq.map (fun (KeyValue(k, v)) -> $"/p:{k}={v}")
        |> fun items -> String.Join(" ", items)
    
    let extraProps = [ "Configuration", configuration.ToString() ]
    let globalProps =
        msbuildProps
        |> Option.defaultValue Map.empty
        |> Seq.map (fun (KeyValue(k, v)) -> k, v)
        |> Seq.toList
        |> List.append extraProps
    
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
        
    do subscribeToChecker slnDir.FullName checker

    member x.RestoreSln() = Build.restoreProject slnPath msbuildPropsStringCommandLineString

    member x.BuildSln() =
        Build.buildProject slnPath None msbuildPropsStringCommandLineString

    member x.LoadProjects() =
        let unfilteredPs =
            workspaceLoader.LoadSln(slnPath)
            |> Seq.toArray
        let ps =
            unfilteredPs
            |> fun ps ->
                match projectFilter with
                | Some filter -> ps |> Array.filter (fun p -> Regex.IsMatch(p.ProjectFileName, filter) = false)
                | None -> ps
        let fsharpProjects =
            ps
            |> Array.filter (fun p -> projectLanguage p.ProjectFileName = ProjectLanguage.FSharp)
        let fcsProjects =
            FCS.mapManyOptions ps
            |> Seq.map (fun p -> p)
            |> Seq.toArray

        projects <-
            Array.zip ps fcsProjects
            |> Array.map (fun (p, fp) ->
                let fcs =
                    match projectLanguage p.ProjectFileName with
                    | ProjectLanguage.FSharp -> Some fp
                    | _ -> None
                p.ProjectFileName, { Project.Raw = p; Project.FCS = fcs }
            )
            |> Map.ofArray
            
        let projectsString =
            projects
            |> Map.toArray
            |> Array.map fst
            |> fun projectNames -> String.Join(Environment.NewLine, projectNames)
        Log.Information($"Loaded {projects.Count} projects: {Environment.NewLine}{projectsString}")

    member x.Projects = projects

    member x.CheckAllFSharpProjects(?inParallel : bool) =
        let inParallel = inParallel |> Option.defaultValue false
        projects
        |> Map.toArray
        |> Array.filter (fun (n, p) -> p.Language = ProjectLanguage.FSharp)
        |> Seq.map (fun (n, p) ->
            async {
                Log.Information("ParseAndCheckProject start | {project}", Path.GetRelativePath(slnDir.FullName, p.Name))
                let res = checker.ParseAndCheckProject(p.FCS.Value) |> Async.StartAsTask |> _.Result
                Log.Information("ParseAndCheckProject end   | {project}", Path.GetRelativePath(slnDir.FullName, p.Name))
                return res
            })
        |> Seq.toArray
        |> fun x -> if inParallel then Async.Parallel x else Async.Sequential x
        |> Async.StartAsTask
        |> fun t ->
            for projRes in t.Result do
                for d in projRes.Diagnostics do
                    let level =
                        match d.Severity with
                        | FSharpDiagnosticSeverity.Hidden -> LogEventLevel.Verbose
                        | FSharpDiagnosticSeverity.Info -> LogEventLevel.Debug
                        | FSharpDiagnosticSeverity.Warning -> LogEventLevel.Warning
                        | FSharpDiagnosticSeverity.Error -> LogEventLevel.Error
                        
                    Log.Write(level,
                        "Diagnostic | {project} | {filename}:{range} | {message}",
                        Path.GetFileName(projRes.ProjectContext.ProjectOptions.ProjectFileName),
                        Path.GetRelativePath(slnDir.FullName, d.FileName),
                        $"{d.Range.StartLine}-{d.Range.EndLine}",
                        d.Message
                    )