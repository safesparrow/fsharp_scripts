#r "nuget: CliWrap, 3.6.0"
#r "nuget: MSBuild.StructuredLogger, 2.1.746"
#r "nuget: Deedle, 3.0.0"
#r "nuget: FSharp.Data"
open FSharp.Data
open System.IO
open Microsoft.Build.Logging.StructuredLogger
open CliWrap
open System
type Row =
    {
        Name : string
        Duration : TimeSpan
    }

type Run =
    {
        Project : string
        StartTime : string
        Duration : TimeSpan
        Id : string
    }   

let go (csv : string) =
    let data =
        CsvFile
            .Load(csv)
            .Cache()
        
    let parseDuration (x : CsvRow) =
        TimeSpan.FromSeconds(System.Double.Parse(x["Duration(s)"]))

    let byId =
        data.Rows
        |> Seq.map (fun x -> x["Id"], x)
        |> readOnlyDict

    let roots =
        data.Rows
        |> Seq.filter (fun x -> x["Name"] = "FSC compilation")
        |> Seq.map (fun x ->
            {
                Project = x["project"]
                StartTime = x["StartTime"]
                Duration = x |> parseDuration 
                Id = x["Id"]
            }        
        )
        |> Seq.toArray

    let rootIds = roots |> Array.map (fun r -> r.Id)

    let rec findRoot (x : CsvRow) =
        if String.IsNullOrEmpty(x["ParentId"]) then
            x["Id"]
        else
            let parent = byId[x["ParentId"]]
            findRoot parent

    let groups =
        data.Rows
        |> Seq.toArray
        |> Array.groupBy findRoot
    
    let sumBy (x : CsvRow seq) (g) =
        x
        |> Seq.groupBy g
        |> Seq.map (fun (key, items) -> key, items |> Seq.sumBy (fun x -> (parseDuration x).TotalMilliseconds))
        |> Seq.toArray
    
    let sums =
        groups
        |> Array.

    let data =
        data.Rows
        |> Seq.filter (fun x -> rootIds |> Array.contains x["ParentId"])
        |> Seq.groupBy (fun x -> x["ParentId"])
        |> readOnlyDict
        |> Seq.filter (fun x -> System.String.IsNullOrEmpty(x["fileName"]))
        |> Seq.map (fun x ->
            {
                Name = x["Name"]
                Duration = x |> parseDuration 
            }
        )
        |> Seq.toArray
    for d in data do
        printfn $"%+A{d}"

match fsi.CommandLineArgs with
| [|_; csv|] -> go csv
| args -> failwith $"Usage: 'script path' csv"