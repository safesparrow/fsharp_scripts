
open Scripts.Sample

[<EntryPoint>]
let main argv =
    Scripts.Utils.setupLogging true
    CompileAFewProjects.run ()
    // let fscDll = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\fsc.dll"
    //let fscDll = @"C:\projekty\fsharp\nojaf\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    // Scripts.Utils.setupLogging (true)
    // TestFcsCompilationDeterminism fscDll
    // testMkArgsDeterminism()
    // TestCheckouts()
    // TestCheckoutsFantomas()
    // TestFindNondeterministicFile2 @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    0