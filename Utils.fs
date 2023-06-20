module Scripts.Utils

open Serilog
open Serilog.Events

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

