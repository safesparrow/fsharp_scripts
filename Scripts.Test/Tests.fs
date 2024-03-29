[<NUnit.Framework.TestFixture>]
module Scripts.Test

open Scripts.More

open System.IO
open NUnit.Framework
open Scripts.Git
open Scripts.Sample
open ArgsFile

[<SetUp>]
let SetUp () =
    Utils.setupLogging true

let config =
    {
        CheckoutsConfig.CacheDir = Path.Combine(Utils.repoDir, ".cache")
    }

[<Test>]
let TestArgs () =
    let argsString = File.ReadAllText (Path.Combine(__SOURCE_DIRECTORY__, "fsc_new.args"))
    let rawArgs = FscArgs.split argsString
    let args = FscArgs.parse rawArgs
    printfn $"{args.Length} args parsed:"
    
    let stringified = FscArgs.stringifyAll args
    Assert.That(stringified, Is.EquivalentTo rawArgs)
    
    let structured = args |> SArgs.structurize
    printfn $"{structured}"
    let destructured = structured |> SArgs.destructurize
    printfn $"{destructured}"
    Assert.That(destructured, Is.EquivalentTo args)  
    
    let modified = structured |> SArgs.clearTestFlag "GraphBasedChecking"
    printfn $"{modified}"
    Assert.That(modified |> SArgs.destructurize |> Array.length, Is.EqualTo (destructured.Length - 1))
