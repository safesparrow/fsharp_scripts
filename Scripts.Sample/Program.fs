open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open FSharp.Compiler.CodeAnalysis
open Scripts.Compiler
open Scripts.Sample
open System
open Scripts.More
open Scripts.ArgsFile
open Ionide
open Ionide.ProjInfo

type Project =
    {
        Raw : Types.ProjectOptions
        FCS : FSharpProjectOptions
    }
    member this.Name = this.Raw.ProjectFileName
    member this.ShortName = Path.GetFileNameWithoutExtension(this.Name)

type IDE(slnPath : string, ?configuration : Configuration) =
    let slnDir = DirectoryInfo(Path.GetDirectoryName(slnPath))
    let toolsPath = Init.init slnDir None
    let globalProps = ["Configuration", configuration.ToString()]
    let workspaceLoader = WorkspaceLoaderViaProjectGraph.Create(toolsPath, globalProps)
    
    let mutable projects : IReadOnlyDictionary<string, Project> = null
    
    member x.LoadProjects() =
        let ps = workspaceLoader.LoadSln(slnPath) |> Seq.toArray
        let fcsProjects = FCS.mapManyOptions ps |> Seq.toArray
        projects <-
            Array.zip ps fcsProjects
            |> Array.map (fun (raw, fcs) ->
                raw.ProjectFileName, { Project.Raw = raw; Project.FCS = fcs }
            )
            |> readOnlyDict
    
    member x.Projects = projects
        

let testIDE() =
    let spec = fantomas
    let dir = SamplePreparation.prepare CompileAFewProjects.config spec
    let slnPath = Path.Combine(dir, "Fantomas.sln")
    let ide = IDE(slnPath)
    let project = Path.Combine(dir, "src", "Fantomas", "Fantomas.fsproj")
    let binlogPath = Path.Combine(Environment.CurrentDirectory, "x.binlog")
    //buildProject project binlogPath ""
    let args = generateCompilationArgs project []
    args |> SArgs.toFile "fantomas.args"    

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
    crackFantomas
    // crackFantomas ()
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