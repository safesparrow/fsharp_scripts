﻿module Scripts.Sample

open System.IO
open System.Management.Automation
open LibGit2Sharp
open Scripts.Git
open Serilog

[<RequireQualifiedAccess>]
type CodebaseSpec =
    | GitHub of CheckoutSpec
    | Local of string
    with
        static member MakeGithub(org : string, repo : string, rev : string) =
            {
                OrgRepo = OrgRepo.Make(org, repo)
                Revision = rev
            }
            |> GitHub

[<RequireQualifiedAccess>]
type PrepareScript =
    | PowerShell of string
    | JustBuild

type Sample =
    {
        CodebaseSpec : CodebaseSpec
        PrepareScript : PrepareScript
    }
    
module SamplePreparation =
    let codebaseDir (config : CheckoutsConfig) (codebase : CodebaseSpec) : string =
        match codebase with
        | CodebaseSpec.GitHub spec -> CheckoutSpec.dir config spec
        | CodebaseSpec.Local dir -> dir
    
    let prepare (config : CheckoutsConfig) (sample : Sample) =
        
        match sample.CodebaseSpec with
        | CodebaseSpec.Local _ -> ()
        | CodebaseSpec.GitHub spec ->
            prepareCheckout config spec
        
        let dir = codebaseDir config sample.CodebaseSpec
        match sample.PrepareScript with
        | PrepareScript.PowerShell script ->
            Log.Information $"Running preparation PowerShell script."
            use ps = PowerShell.Create()
            ps.AddScript($"cd {dir}") |> ignore
            let results = ps.AddScript(script).Invoke()
            results
            |> Seq.iter (fun r -> Log.Information(r.ToString()))
            if ps.HadErrors then
                failwith "PowerShell preparation script failed."
            ()
        | PrepareScript.JustBuild ->
            ()
