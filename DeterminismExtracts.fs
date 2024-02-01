module Scripts.DeterminismExtracts

open System
open System.IO
open System.Security.Cryptography
open Newtonsoft.Json
open Scripts.ArgsFile
open Serilog
open Scripts.Utils

[<CLIMutable>]
type ExtractCore =
    {
        Mvid : string
        DllHash : string
        PdbHash : string
        RefHash : string
        Project : string
        SigDataHash : string
    }

[<CLIMutable>]
type ExtractMeta =
    {
        DllTimestamp : DateTime
        Name : string
        Directory : string
    }

[<CLIMutable>]
type Extract =
    {
        Core : ExtractCore
        Meta : ExtractMeta
    }

/// Paths of various files generated during compilation.
module Paths =
    let mvid = "mvid.txt"
    let dll = "out.dll"
    let pdb = "out.pdb"
    let ref = "ref.dll"
    let args = "fscargs.txt"
    let extract = "extract.json"
    let sigData : string = "out.signature-data.json"

let getFileHash (file : string) =
    if not (File.Exists(file)) then
        "File does not exist"
    else
        use md5 = MD5.Create()
        use stream = File.OpenRead(file)
        let hash = md5.ComputeHash(stream)
        BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()

let getExtract (project : string) (extractName : string) (dir : string) =
    let subPath name = Path.Combine(dir, name)
    let dll = subPath Paths.dll
    {
        Extract.Core =
            {
                ExtractCore.Mvid = File.ReadAllText(subPath Paths.mvid).Trim()
                DllHash = getFileHash dll
                PdbHash = getFileHash (subPath Paths.pdb)
                RefHash = getFileHash (subPath Paths.ref)
                Project = project
                SigDataHash = getFileHash (subPath Paths.sigData)
            }
        Extract.Meta =
            {
                ExtractMeta.Directory = dir
                DllTimestamp = File.GetLastWriteTimeUtc(dll)
                Name = extractName
            }
    }
    

type ProjectWithArgs =
    {
        /// fsproj path
        Project : string
        Args : SArgs
    }

/// Compile a project and extract information out of it helpful in determinism investigations
let compileAndExtract
    (useTmpDir : bool)
    (name : string)
    (baseDir : string)
    (projectArgs : ProjectWithArgs)
    (fscDll : string)
    : Extract
    =
    let finalDir = Path.Combine(baseDir, name)
    let outputDir =
        if useTmpDir then
            Log.Information("compileAndExtract {finalDir}", finalDir)
            "testoutput"
        else
            finalDir
    let outputDir = Path.Combine(Environment.CurrentDirectory, outputDir)
    Directory.CreateDirectory(outputDir) |> ignore
    let subPath name = Path.Combine(outputDir, name)
    let {Project = project; Args = args} = projectArgs
    let workDir = Path.GetDirectoryName(project)

    let dllPath = subPath Paths.dll
    let args =
        args
        |> SArgs.setOutput (Path.GetRelativePath(workDir, subPath Paths.dll))
        |> SArgs.setKeyValue "--refout" (Path.GetRelativePath(workDir, subPath Paths.ref))
        |> SArgs.setKeyValue "--debug" "portable"
    
    let argsFile = subPath Paths.args
    args
    |> SArgs.toFile argsFile
    
    CliWrap.Cli
        .Wrap("dotnet")
        .WithWorkingDirectory(workDir)
        .WithArguments($"{fscDll} @{argsFile}")
        .ExecuteAssertSuccess()
    
    let mvid = MvidReader.getMvid dllPath
    let mvidPath = subPath Paths.mvid
    File.WriteAllText(mvidPath, mvid.ToString())
    
    let extract = getExtract project name outputDir
    
    let json = JsonConvert.SerializeObject(extract, Formatting.Indented)
    File.WriteAllText(subPath Paths.extract, json)
    
    if useTmpDir then
        if Directory.Exists finalDir then
            failwith $"Output directory {finalDir} exists."
            
        // Make sure parent directory exists
        Directory.CreateDirectory(finalDir) |> ignore
        Directory.Delete(finalDir)
        Directory.Move(outputDir, finalDir)
    
    extract

