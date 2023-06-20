module Scripts.Utils

open Serilog
open Serilog.Events
open CliWrap

let setupLogging (verbose: bool) =
    Log.Logger <-
        LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo
            .Console(
                outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {step:j}: {Message:lj}{NewLine}{Exception}"
            )
            .MinimumLevel
            .Is(
                if verbose then
                    LogEventLevel.Verbose
                else
                    LogEventLevel.Information
            )
            .CreateLogger()

let repoDir = __SOURCE_DIRECTORY__

type Command with
    member this.ExecuteAssertSuccess() =
        this
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .WithStandardErrorPipe(PipeTarget.ToFile "stderr.txt")
            .WithStandardOutputPipe(PipeTarget.ToFile "stdout.txt")
            .ExecuteAsync()
            .GetAwaiter()
            .GetResult()
        |> ignore
            