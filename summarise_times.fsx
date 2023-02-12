#r "nuget: CliWrap, 3.6.0"
#r "nuget: MSBuild.StructuredLogger, 2.1.746"
#r "nuget: Deedle, 3.0.0"
#r "nuget: FSharp.Data"
#I "C:/Users/janus/.nuget/packages/deedle/3.0.0"
#load "Deedle.fsx"
#r "nuget: Microsoft.Data.Analysis, 0.20.1"

open FSharp.Data
open System.IO
open CliWrap
open System

let f () =
    

let go (csv : string) =
    let df = Frame.ReadCsv(csv)        
    df
go "C:/projekty/fsharp/.cache/fsprojects__fantomas/18f31541/src/Fantomas/times.csv"

    // for d in df do
    //     printfn $"%+A{d}"

    // let parseDuration (x : CsvRow) =
    //     TimeSpan.FromSeconds(System.Double.Parse(x["Duration(s)"]))

    // let byId =
    //     data.Rows
    //     |> Seq.map (fun x -> x["Id"], x)
    //     |> readOnlyDict

    // let roots =
    //     data.Rows
    //     |> Seq.filter (fun x -> x["Name"] = "FSC compilation")
    //     |> Seq.map (fun x ->
    //         {
    //             Project = x["project"]
    //             StartTime = x["StartTime"]
    //             Duration = x |> parseDuration 
    //             Id = x["Id"]
    //         }        
    //     )
    //     |> Seq.toArray

    // let rootIds = roots |> Array.map (fun r -> r.Id)

    // let summary

    // let groups =
    //     data.Rows
    //     |> Seq.toArray
    //     |> Array.groupBy findRoot
    
    // let sumBy (x : CsvRow seq) (g) =
    //     x
    //     |> Seq.groupBy g
    //     |> Seq.map (fun (key, items) ->
    //         key,
    //         items |> Seq.sumBy (fun x -> (parseDuration x).TotalMilliseconds)
    //     )
    //     |> Seq.toArray
    
    // let sums =
    //     groups
        

    // let data =
    //     data.Rows
    //     |> Seq.filter (fun x -> rootIds |> Array.contains x["ParentId"])
    //     |> Seq.filter (fun x -> System.String.IsNullOrEmpty(x["fileName"]))
    //     |> Seq.groupBy (fun x -> x["ParentId"])
    //     |> readOnlyDict
    //     |> Seq.map (fun x ->
    //         {
    //             Name = x["Name"]
    //             Duration = x |> parseDuration 
    //         }
    //     )
    //     |> Seq.toArray
    // for d in data do
    //     printfn $"%+A{d}"

// match fsi.CommandLineArgs with
// | [|_; csv|] -> go csv
// | args -> failwith $"Usage: 'script path' csv"


// type Row =
//     {
//         Name : string
//         Duration : TimeSpan
//     }

// type Run =
//     {
//         Project : string
//         StartTime : string
//         Duration : TimeSpan
//         Id : string
//     }   

// type Metrics = Metrics of Map<string, double>
//     with
//         member this.Value = match this with Metrics m -> m
//         member this.Get(name) = this.Value |> Map.find name
//         static member Average(items : Metrics[]) =
//             items
//             |> Array.collect (fun m -> m.Value |> Seq.toArray)
//             |> Array.map (fun (KeyValue(k,v)) -> k, v)
//             |> Array.groupBy (fun (k,v) -> k)
//             |> Array.map (fun (k, values) -> k, values |> Array.map snd |> Array.average)
//             |> Map.ofArray

// type EntryKey =
//     {
//         Name : string
//         FileName : string
//     }
// type Entry =
//     {
//         Key : EntryKey
//         Metrics : Metrics
//     }

// let rootKey = {Name = "FCS Compilation"; FileName = null}
// type Summary =
//     {
//         Entries : Entry[]
//         Project : string
//     }
// module Summary =
//     let find (key : EntryKey) (summary : Summary) =
//         summary.Entries |> Array.find (fun e -> e.Key = key)
//     let root (summary : Summary) = summary |> find rootKey


// let rec findRoot (x : CsvRow) =
//     if String.IsNullOrEmpty(x["ParentId"]) then
//         x["Id"]
//     else
//         let parent = byId[x["ParentId"]]
//         findRoot parent


// let extractSummaries (entries : CsvRow[]) : Summary[] =
    


// let go (csv : string) =
//     let data =
//         CsvFile
//             .Load(csv)
//             .Cache()
        
//     let parseDuration (x : CsvRow) =
//         TimeSpan.FromSeconds(System.Double.Parse(x["Duration(s)"]))

//     let byId =
//         data.Rows
//         |> Seq.map (fun x -> x["Id"], x)
//         |> readOnlyDict

//     let roots =
//         data.Rows
//         |> Seq.filter (fun x -> x["Name"] = "FSC compilation")
//         |> Seq.map (fun x ->
//             {
//                 Project = x["project"]
//                 StartTime = x["StartTime"]
//                 Duration = x |> parseDuration 
//                 Id = x["Id"]
//             }        
//         )
//         |> Seq.toArray

//     let rootIds = roots |> Array.map (fun r -> r.Id)

//     let summary

//     let groups =
//         data.Rows
//         |> Seq.toArray
//         |> Array.groupBy findRoot
    
//     let sumBy (x : CsvRow seq) (g) =
//         x
//         |> Seq.groupBy g
//         |> Seq.map (fun (key, items) ->
//             key,
//             items |> Seq.sumBy (fun x -> (parseDuration x).TotalMilliseconds)
//         )
//         |> Seq.toArray
    
//     let sums =
//         groups
        

//     let data =
//         data.Rows
//         |> Seq.filter (fun x -> rootIds |> Array.contains x["ParentId"])
//         |> Seq.filter (fun x -> System.String.IsNullOrEmpty(x["fileName"]))
//         |> Seq.groupBy (fun x -> x["ParentId"])
//         |> readOnlyDict
//         |> Seq.map (fun x ->
//             {
//                 Name = x["Name"]
//                 Duration = x |> parseDuration 
//             }
//         )
//         |> Seq.toArray
//     for d in data do
//         printfn $"%+A{d}"

// match fsi.CommandLineArgs with
// | [|_; csv|] -> go csv
// | args -> failwith $"Usage: 'script path' csv"