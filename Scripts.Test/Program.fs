open Scripts.Test
open System.IO
open Newtonsoft.Json.Serialization
open Newtonsoft.Json.Linq

let getReportWarningsTyparFromJson (path : string) =
    let json = File.ReadAllText(path)
    let o = JObject.Parse(json)
    let x = o.SelectToken("$..children[?(@.name == 'ReportWarnings')].children[?(@.kind == 'Typar')].name")
    if x = null then None
    else x.Value<string>() |> Some

[<EntryPoint>]
let main argv =
    // TestCheckouts()
    TestFindNondeterministicFile ()
    0