open System.Collections.Generic
open System.Collections.Immutable
open System.Diagnostics
open System.IO
open FSharp.Compiler.CodeAnalysis
open Ionide.ProjInfo.Types
open OpenTelemetry
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open Scripts.Compiler
open Scripts.Sample
open System
open Scripts.More
open Scripts.ArgsFile
open Ionide
open Ionide.ProjInfo
open Serilog

type Project =
    {
        Raw : ProjectOptions
        FCS : FSharpProjectOptions
    }
    member this.Name = this.Raw.ProjectFileName
    member this.ShortName = Path.GetFileNameWithoutExtension(this.Name)

type FSharpProjectOptions
    with member x.ProjectDir = Path.GetDirectoryName(x.ProjectFileName) 

let pathRelativeToProject (op : FSharpProjectOptions) (filename : string) =
    Path.GetRelativePath(op.ProjectDir, filename)

let private subscribeToChecker (checker : FSharpChecker) =
    checker.FileChecked.AddHandler(fun (sender : obj) (filename : string, op : FSharpProjectOptions) ->
        let name = op.ProjectFileName |> Path.GetFileNameWithoutExtension
        Log.Information("{project} | FileChecked {file}", name.PadRight(20), pathRelativeToProject op filename)
        ())
    
    checker.FileParsed.AddHandler(fun (sender : obj) (filename : string, op : FSharpProjectOptions) ->
        let name = op.ProjectFileName |> Path.GetFileNameWithoutExtension
        Log.Information("{project} | FileParsed  {file}", name.PadRight(20), pathRelativeToProject op filename)
        ())

    checker.ProjectChecked.AddHandler(fun (sender : obj) (op : FSharpProjectOptions) ->
        let name = op.ProjectFileName |> Path.GetFileNameWithoutExtension
        Log.Information("{project} | ProjectChecked", name.PadRight(20))
        ())
    
let mapPath (p : string) : string =
    p
    
let rec fixFSharpOptionsPaths (x : FSharpProjectOptions[]) =
    let dict = Dictionary<string, FSharpProjectOptions>()
    let rec fix (o : FSharpProjectOptions) =
        match dict.TryGetValue o.ProjectFileName with
        | (true, o) -> o
        | (false, _) ->
            let res =
                {
                    o with
                        ReferencedProjects =
                            o.ReferencedProjects
                            |> Array.map (fun rp ->
                                match rp with
                                | FSharpReferencedProject.FSharpReference (o, opt) ->
                                    FSharpReferencedProject.FSharpReference (mapPath o, fix opt)
                                | x -> x
                            )
                }
            dict[o.ProjectFileName] <- res
            res
    x |> Array.map fix
    
type IDE(slnPath : string, ?configuration : Configuration) =
    let configuration = configuration |> Option.defaultValue Configuration.Debug 
    let slnDir = DirectoryInfo(Path.GetDirectoryName(slnPath))
    let toolsPath = Init.init slnDir None
    let globalProps = ["Configuration", configuration.ToString()]
    let workspaceLoader = WorkspaceLoaderViaProjectGraph.Create(toolsPath, globalProps)
    let mutable projects : Map<string, Project> = Map.empty
    
    let checker = FSharpChecker.Create()
    do
        subscribeToChecker checker
    
    member x.LoadProjects() =
        let ps = workspaceLoader.LoadSln(slnPath) |> Seq.toArray
        let fcsProjects = FCS.mapManyOptions ps |> Seq.toArray
        projects <-
            Array.zip ps fcsProjects
            |> Array.map (fun (raw, fcs) ->
                raw.ProjectFileName, { Project.Raw = raw; Project.FCS = fcs }
            )
            |> Map.ofArray
        
        Log.Information("Getting ready")
        projects
        |> Seq.map (fun (KeyValue(n, p)) ->
            async {
                if p.Name.Contains("Fantomas.Core.Tests") then
                    let res = checker.ParseAndCheckProject(p.FCS) |> Async.StartAsTask |> _.Result
                    for d in res.Diagnostics do
                        Log.Information("Diagnostic | {filename} | {message}", d.FileName, d.Message)
                    ()
                return ()
            }
        )
        |> Seq.toArray
        |> Async.Parallel
        |> Async.StartAsTask
        |> fun t -> t.Result
    
    member x.Projects = projects

let setupTelemetry () =
    Sdk
        .CreateTracerProviderBuilder()
        .AddSource("fsc")
        .SetResourceBuilder(
            ResourceBuilder
                .CreateDefault()
                .AddService (serviceName = "program", serviceVersion = "42.42.42.44")
        )
        .AddOtlpExporter(fun c ->
            c.ExportProcessorType <- ExportProcessorType.Batch
            let o = BatchExportProcessorOptions()
            o.MaxQueueSize <- 100000
            c.BatchExportProcessorOptions <- o
        )
        .Build ()

let testIDE() =
    let spec = fantomas
    let dir = SamplePreparation.prepare CompileAFewProjects.config spec
    let slnPath = Path.Combine(dir, "Fantomas.sln")
    let ide = IDE(slnPath)
    ide.LoadProjects()

let crackFantomas () =
    let spec = fantomas
    let dir = SamplePreparation.prepare CompileAFewProjects.config spec
    let project = Path.Combine(dir, "src", "Fantomas", "Fantomas.fsproj")
    let binlogPath = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    //buildProject project binlogPath ""
    let args = generateCompilationArgs project []
    args |> SArgs.toFile "fantomas.args"

let activitySource = ActivitySource("fsc")

[<EntryPoint>]
let main argv =
    Scripts.Utils.setupLogging true
    use tracerProvider = setupTelemetry ()
    use trace = activitySource.StartActivity("testIDE", ActivityKind.Internal)
    testIDE ()
    0