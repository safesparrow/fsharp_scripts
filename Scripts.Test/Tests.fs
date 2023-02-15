[<NUnit.Framework.TestFixture>]
module Scripts.Test

open System
open System.IO
open System.Security.Cryptography
open NUnit.Framework
open Scripts.Git
open Scripts.Sample
open ArgsFile    

let config =
    {
        CheckoutsConfig.CacheDir =  "c:/projekty/fsharp/fsharp_scripts/.cache"
    }

[<Test>]
let TestArgs () =
    let argsString = File.ReadAllText @"C:\projekty\fsharp\fsharp_scripts\fsc.args"
    let rawArgs = FscArgs.split argsString
    let args = FscArgs.parse rawArgs
    printfn $"{args.Length} args parsed:"
    printfn $"%+A{args}"
    
    let stringified = FscArgs.stringifyAll args
    Assert.That(stringified, Is.EquivalentTo rawArgs)
    
    let structured = args |> SArgs.structurize
    printfn $"{structured}"
    let destructured = structured |> SArgs.destructurize
    printfn $"{destructured}"
    Assert.That(destructured, Is.EquivalentTo args)  
    
    let modified = structured |> SArgs.clearTestFlag "ParallelCheckingWithSignatureFilesOn"
    printfn $"{modified}"
    Assert.That(modified |> SArgs.destructurize |> Array.length, Is.EqualTo (destructured.Length - 1))

type ProjectSArgs =
    {
        Project : string // fsproj path
        Args : SArgs
    }

[<Test>]
let TestCheckouts () =

    let sample =
        {
            Sample.CodebaseSpec = CodebaseSpec.GitHub (CheckoutSpec.Make("fsprojects", "fantomas", "18f31541e983c9301e6a55ba6582817bc704cb6f"))
            PrepareScript = PrepareScript.JustBuild
        }
    SamplePreparation.prepare config sample
    
    let baseDir = Sample.SamplePreparation.codebaseDir config sample.CodebaseSpec
    let projFile = Path.Combine(baseDir, "src/Fantomas/Fantomas.fsproj")
    mkArgsFile projFile (Path.Combine(__SOURCE_DIRECTORY__, "fantomas.args"))

type ExtractCore =
    {
        Mvid : string
        DllHash : string
        PdbHash : string
        RefHash : string
    }

type ExtractMeta =
    {
        DllTimestamp : DateTime
        Name : string
        Directory : string
    }

type Extract =
    {
        Core : ExtractCore
        Meta : ExtractMeta
    }

module Paths =
    let mvid = "mvid.txt"
    let dll = "out.dll"
    let pdb = "out.pdb"
    let ref = "ref.dll"
    let args = "fscargs.txt"
    let extract = "extract.json"

let getFileHash (file : string) =
    if not (File.Exists(file)) then
        failwith $"File '{file}' does not exist"
    use md5 = MD5.Create()
    use stream = File.OpenRead(file)
    let hash = md5.ComputeHash(stream)
    BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()

let getExtract (name : string) (dir : string) =
    let subPath name = Path.Combine(dir, name)
    let dll = subPath Paths.dll
    {
        Extract.Core =
            {
                ExtractCore.Mvid = File.ReadAllText(subPath Paths.mvid).Trim()
                DllHash = getFileHash dll
                PdbHash = getFileHash (subPath Paths.pdb)
                RefHash = getFileHash (subPath Paths.ref)
            }
        Extract.Meta =
            {
                ExtractMeta.Directory = dir
                DllTimestamp = File.GetLastWriteTimeUtc(dll)
                Name = name
            }
    }

let go (outputDir : string) (projectArgs : ProjectSArgs) (fscDll : string) =
    Directory.CreateDirectory(outputDir) |> ignore
    let subPath name = Path.Combine(Environment.CurrentDirectory, Path.Combine(outputDir, name))
    let dllPath = subPath Paths.dll
    let {Project = project; Args = args} = projectArgs
    let args =
        args
        |> SArgs.setOutput dllPath
        |> SArgs.setKeyValue "--refout" (subPath Paths.ref)
        |> SArgs.setKeyValue "--debug" "portable"
    
    let argsFile = subPath Paths.args
    args
    |> SArgs.toFile argsFile
    
    CliWrap.Cli
        .Wrap("dotnet")
        .WithWorkingDirectory(Path.GetDirectoryName(project))
        .WithArguments($"{fscDll} @{argsFile}")
        .ExecuteAssertSuccess()
    
    let mvid = MvidReader.getMvid dllPath
    let mvidPath = subPath Paths.mvid
    File.WriteAllText(mvidPath, mvid.ToString())
    
[<Test>]
let TestFindNondeterministicFile () =
    // We have FSC args that we know produce non-deterministic results.
    let path = Path.Combine(__SOURCE_DIRECTORY__, "fantomas.args")
    let args = SArgs.ofFile path
    let project = @"c:\projekty\fsharp\fsharp_scripts\.cache\fsprojects__fantomas\18f31541\src\Fantomas\Fantomas.fsproj"
    let projectArgs =
        {
            Project = project
            Args = args
        }
    printfn $"%+A{args}"
    let dir = "test_output"
    let fsc = @"C:\projekty\fsharp\nojaf\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.dll"
    
    go dir projectArgs fsc
   