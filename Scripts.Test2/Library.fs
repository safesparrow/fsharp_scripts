[<NUnit.Framework.TestFixture>]
module Scripts.Test

open System.IO
open NUnit.Framework

[<Test>]
let hello () =
    let argsString = File.ReadAllText @"C:\projekty\fsharp\scripts\fsc.args"
    let args = ArgsFile.FscArgs.parse argsString
    printfn $"{args.Length} args parsed:"
    printfn $"%+A{args}"