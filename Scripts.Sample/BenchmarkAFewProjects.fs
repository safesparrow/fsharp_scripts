module Scripts.Sample.BenchmarkAFewProjects
open System.IO
open Scripts
open Scripts.Sample
open Scripts.Git

let run () =
    let config =
        {
            CheckoutsConfig.CacheDir = Path.Combine(Utils.repoDir, ".cache")
        }
        
    let fsharp =
        {
            Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("safesparrow", "fsharp", "948f8b2f9cea960c4286bf957ef0e3a1c591ed0f")
            PrepareScript = PrepareScript.JustBuild
        }
        
    let dir = SamplePreparation.prepare config fsharp
    Compiler.build dir