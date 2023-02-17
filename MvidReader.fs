module Scripts.MvidReader

open System
open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable



let getMvid (dll: string): Guid =
    
    if not (dll.EndsWith(".dll")) then
        failwithf $"Expected %s{dll} to have .dll extension"

    if not (File.Exists dll) then
        failwithf $"%s{dll} does not exist on disk"

    use embeddedReader = new PEReader(File.OpenRead dll)
    let sourceReader = embeddedReader.GetMetadataReader()
    let loc = sourceReader.GetModuleDefinition().Mvid
    let mvid = sourceReader.GetGuid(loc)
    
    let handleToString (sh : StringHandle) : string =
        if sh.IsNil then "" else sourceReader.GetString(sh)
    // let resources =
    //     sourceReader.ManifestResources
    //     |> Seq.toArray
    //     |> Array.map (fun h -> sourceReader.GetManifestResource(h))
    // let sigData = resources |> Array.find (fun r -> (r.Name |> handleToString) = "FSharpSignatureData.out")
    // use f = new ICSharpCode.Decompiler.Metadata.PEFile(dll)
    
    mvid
