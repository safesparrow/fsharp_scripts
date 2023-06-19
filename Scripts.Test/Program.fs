open MBrace.FsPickler
open Scripts.Test
open System.IO
open Newtonsoft.Json.Linq

let getReportWarningsTyparFromJson (path : string) =
    let json = File.ReadAllText(path)
    let o = JObject.Parse(json)
    let x = o.SelectToken("$..children[?(@.name == 'ReportWarnings')].children[?(@.kind == 'Typar')].name")
    if x = null then None
    else x.Value<string>() |> Some

[<EntryPoint>]
let main argv =
    // let fscDll = @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\fsc.dll"
    //let fscDll = @"C:\projekty\fsharp\nojaf\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    // Scripts.Utils.setupLogging (true)
    // TestFcsCompilationDeterminism fscDll
    // testMkArgsDeterminism()
    // TestCheckouts()
    // TestCheckoutsFantomas()
    // TestFindNondeterministicFile2 @"C:\projekty\fsharp\fsharp_scripts\.cache\dotnet__fsharp\40916855\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    0