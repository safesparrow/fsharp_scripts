[<NUnit.Framework.TestFixture>]
module Scripts.Test

open System.IO
open NUnit.Framework
open Scripts.Git
open Scripts.Sample
//
// [<Test>]
// let hello () =
//     let argsString = File.ReadAllText @"C:\projekty\fsharp\scripts\fsc.args"
//     let args = ArgsFile.FscArgs.parse argsString
//     printfn $"{args.Length} args parsed:"
//     printfn $"%+A{args}"

[<Test>]
let foo () =
    let config =
        {
            CheckoutsConfig.CacheDir =  "c:/projekty/fsharp/funky/Lo.l../.cache"
        }
    let sample =
        {
            Sample.CodebaseSpec = CodebaseSpec.GitHub (CheckoutSpec.Make("dotnet", "fantomas", "18f31541e983c9301e6a55ba6582817bc704cb6f"))
            PrepareScript = PrepareScript.JustBuild   
        }
    SamplePreparation.prepare config sample
