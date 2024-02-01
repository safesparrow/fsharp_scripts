open System.IO
open Scripts.Sample
open System
open Scripts.More
open Scripts.ArgsFile

let crackFantomas () =
    let spec = fantomas
    let dir = SamplePreparation.prepare CompileAFewProjects.config spec
    let project = Path.Combine(dir, "src", "Fantomas", "Fantomas.fsproj")
    
    let binlogPath = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    //buildProject project binlogPath ""
    let args = generateCompilationArgs project []
    args |> SArgs.toFile "fantomas.args"

[<EntryPoint>]
let main argv =
    Scripts.Utils.setupLogging true
    crackFantomas ()
    //CompileAFewProjects.run ()
    // let fscDll = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\fsc.dll"
    //let fscDll = @"C:\projekty\fsharp\nojaf\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    // Scripts.Utils.setupLogging (true)
    // TestFcsCompilationDeterminism fscDll
    // testMkArgsDeterminism()
    // TestCheckouts()
    // TestCheckoutsFantomas()
    // TestFindNondeterministicFile2 @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    0