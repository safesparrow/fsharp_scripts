module Scripts.Sample

open System
open System.IO
open System.Management.Automation
open Scripts.Git
open Serilog

[<RequireQualifiedAccess>]
type CodebaseSpec =
    | GitHub of CheckoutSpec
    | Local of string
    with
        static member MakeGithub(org : string, repo : string, rev : string, ?suffix : string) =
            {
                OrgRepo = OrgRepo.Make(org, repo)
                Revision = rev
                Suffix = suffix
            }
            |> GitHub

[<RequireQualifiedAccess>]
type PrepareScript =
    | PowerShell of string
    | Nothing

type Sample =
    {
        Name : string
        CodebaseSpec : CodebaseSpec
        PrepareScript : PrepareScript
        MainSolution : string
        MSBuildProps : Map<string, string>
        SDKRequirementsDescription : string
    }

type AllConfig =
    {
        CacheDir : string
    }
    
module SamplePreparation =
    let codebaseDir (config : CheckoutsConfig) (codebase : CodebaseSpec) : string =
        match codebase with
        | CodebaseSpec.GitHub spec -> specDir config spec
        | CodebaseSpec.Local dir -> dir
    
    let preparationMarkerPath (config : CheckoutsConfig) (spec : CheckoutSpec) =
        (specDir config spec) + ".marker"
    
    let prepare (config : CheckoutsConfig) (sample : Sample) =        
        match sample.CodebaseSpec with
        | CodebaseSpec.Local _ -> ()
        | CodebaseSpec.GitHub spec ->
            prepareCheckout config spec
        
        let prepare (dir : string) (script : string) =
            Log.Information $"Running codebase preparation PowerShell script."
            use ps = PowerShell.Create()
            ps.AddScript($"cd {dir}") |> ignore
            let results = ps.AddScript(script).Invoke()
            results
            |> Seq.iter (fun r -> Log.Information(r.ToString()))
            if ps.HadErrors then
                failwith "PowerShell preparation script failed."
        
        let dir = codebaseDir config sample.CodebaseSpec
        
        match sample.PrepareScript with
        | PrepareScript.PowerShell script ->
            match sample.CodebaseSpec with
            | CodebaseSpec.Local _ ->
                prepare dir script
            | CodebaseSpec.GitHub spec ->
                let markerPath = preparationMarkerPath config spec
                if File.Exists(markerPath) then
                    Log.Information("Skipping preparation script as preparation marker file exists: {markerPath}", markerPath)
                else
                    prepare dir script
                    File.WriteAllText(markerPath, DateTime.Now.ToString("o"))
                    Log.Information("Preparation finished. Marker file created: {markerPath}", markerPath)
                ()
            ()
        | PrepareScript.Nothing ->
            ()
        dir