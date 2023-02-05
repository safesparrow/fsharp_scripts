#r "nuget: System.Reflection.Metadata"

open System
open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable

let getMvid refDll =
    use embeddedReader = new PEReader(File.OpenRead refDll)
    let sourceReader = embeddedReader.GetMetadataReader()
    let loc = sourceReader.GetModuleDefinition().Mvid
    let mvid = sourceReader.GetGuid(loc)
    printfn "%s at %s" (mvid.ToString()) (DateTime.Now.ToString())


let dll : string = Array.last fsi.CommandLineArgs


if not (dll.EndsWith(".dll")) then
    failwithf "Expected %s to have .dll extension" dll

if not (File.Exists dll) then
    failwithf "%s does not exist on disk" dll

getMvid dll