module Scripts.DeterminismExtracts

open System
open System.IO
open System.Security.Cryptography

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

let getExtract (project : string) (name : string) (dir : string) =
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
                Name = name
            }
    }
