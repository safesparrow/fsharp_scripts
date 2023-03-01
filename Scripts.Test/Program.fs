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
    Scripts.Utils.setupLogging (true)
    testMkArgsDeterminism()
    // TestCheckouts()
    // TestCheckoutsFantomas()
    // TestFindNondeterministicFile2 ()
    0