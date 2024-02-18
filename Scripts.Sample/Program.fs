module Scripts.Sample.Program

open System
open System.Diagnostics
open System.IO
open OpenTelemetry
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open Scripts.Git
open Scripts.More
open Scripts.IDE
open Serilog

let setupTelemetry () =
    Sdk
        .CreateTracerProviderBuilder()
        .AddSource("fsc")
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName = "Scripts.Sample"))
        .AddOtlpExporter(fun c ->
            c.ExportProcessorType <- ExportProcessorType.Batch
            let o = BatchExportProcessorOptions()
            o.MaxQueueSize <- 100000
            c.BatchExportProcessorOptions <- o)
        .Build()

let config =
    { CheckoutsConfig.CacheDir = Path.Combine(Scripts.Utils.repoDir, ".cache") }

let activitySource = new ActivitySource("fsc")

let testIDE (spec : Sample) (transparentCompiler: bool) =
    Log.Information($"TestIDE: Sample {spec.Name}, Use TC={transparentCompiler}")
    use trace = activitySource.StartActivity("testIDE", ActivityKind.Internal)
    trace.SetTag("transparentCompiler", transparentCompiler) |> ignore
    let dir = SamplePreparation.prepare config spec
    let slnPath = Path.Combine(dir, spec.MainSolution)

    let ide =
        IDE(slnPath,
            checkerOptionsOverrides = (fun opts -> { opts with UseTransparentCompiler = transparentCompiler }),
            msbuildProps = spec.MSBuildProps
        )

    ide.RestoreSln()
    ide.LoadProjects()
    ide.CheckAllProjectsInParallel()

open Argu

type TCModes =
    | On
    | Off
    | Both

type Args =
    | [<Mandatory>] Sample of string
    | TCModes of TCModes
    | Tracing of bool

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Sample _ ->
                let samplesString =
                    Scripts.More.Samples.all |> Seq.map (fun s -> s.Name) |> String.concat ", "

                $"Name of the sample. Recognised samples: {samplesString}"
            | TCModes _ -> $"Run with and/or without TransparentCompiler"
            | Tracing _ -> $"Produce OpenTelemetry traces and send them to local Jaeger port"

let dummyDisposable =
    { new IDisposable with
        member this.Dispose() = () }

[<EntryPoint>]
let main argv =
    let parser = Argu.ArgumentParser.Create<Args>()
    let results = parser.Parse(argv)
    let sampleName = results.GetResult(Sample)

    let sample =
        Samples.all
        |> List.tryFind (fun s -> System.String.Equals(s.Name, sampleName, StringComparison.OrdinalIgnoreCase))
        |> Option.defaultWith (fun () -> failwith $"Unknown sample name {sampleName}")

    let tc = results.GetResult(Args.TCModes, defaultValue=TCModes.On)
    Scripts.Utils.setupLogging true

    let useTracing = results.GetResult(Args.Tracing, defaultValue=true)
    use tracerProvider = if useTracing then (setupTelemetry () :> IDisposable) else dummyDisposable
    
    match tc with
    | TCModes.On ->
        testIDE sample true
    | TCModes.Off ->
        testIDE sample false
    | TCModes.Both ->
        testIDE sample true
        testIDE sample false
    0
