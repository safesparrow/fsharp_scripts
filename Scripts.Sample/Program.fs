module Scripts.Sample.Program

open System.Diagnostics
open System.IO
open OpenTelemetry
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open Scripts.Git
open Scripts.More
open Scripts.IDE

let setupTelemetry () =
    Sdk
        .CreateTracerProviderBuilder()
        .AddSource("fsc")
        .SetResourceBuilder(
            ResourceBuilder
                .CreateDefault()
                .AddService(serviceName = "Scripts.Sample")
        )
        .AddOtlpExporter(fun c ->
            c.ExportProcessorType <- ExportProcessorType.Batch
            let o = BatchExportProcessorOptions()
            o.MaxQueueSize <- 100000
            c.BatchExportProcessorOptions <- o)
        .Build()

let config = { CheckoutsConfig.CacheDir = Path.Combine(Scripts.Utils.repoDir, ".cache") }
    
let activitySource = new ActivitySource("fsc")

let testIDE (transparentCompiler : bool) =
    use trace = activitySource.StartActivity("testIDE", ActivityKind.Internal)
    trace.SetTag("transparentCompiler", transparentCompiler) |> ignore
    let spec = fantomas
    let dir = SamplePreparation.prepare config spec
    let slnPath = Path.Combine(dir, "Fantomas.sln")
    let ide = IDE(slnPath, checkerOptionsOverrides = fun opts -> { opts with UseTransparentCompiler = transparentCompiler })
    ide.RestoreSln()
    ide.LoadProjects()
    ide.CheckAllProjectsInParallel()

[<EntryPoint>]
let main argv =
    Scripts.Utils.setupLogging true
    use tracerProvider = setupTelemetry ()
    testIDE true
    testIDE false
    0
