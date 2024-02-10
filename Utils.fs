module Scripts.Utils

open Serilog
open Serilog.Events
open CliWrap
open Serilog.Filters

let setupLogging (verbose: bool) =
    Log.Logger <-
        LoggerConfiguration()
            .Enrich.FromLogContext()
            .Filter.ByExcluding(Matching.FromSource("MsBuild"))
            .WriteTo
            .Console(
                outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {step:j}: {Message:lj}{NewLine}{Exception}"
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
        let command =
            this
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .WithStandardErrorPipe(PipeTarget.ToFile "stderr.txt")
                .WithStandardOutputPipe(PipeTarget.ToFile "stdout.txt")
        Log.Information("Running '{path} {args}' in {dir}", command.TargetFilePath, command.Arguments, command.WorkingDirPath) 
        command
            .ExecuteAsync()
            .GetAwaiter()
            .GetResult()
        |> ignore
            