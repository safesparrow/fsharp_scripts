module Scripts.Samples

open Scripts.Sample

let fantomas =
    {
        Name = "Fantomas"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "Fantomas", "de8ac507903bf545211eaa0efd88c2436fee1424")
        PrepareScript = PrepareScript.Nothing
        MSBuildProps = Map.empty
        MainSolution = "Fantomas.sln"
        SDKRequirementsDescription = "" 
    }
    
let argu =
    {
        Name = "Argu"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "argu", "71625aec0b4b826ab2b4178f3ffa05d294d22840")
        PrepareScript = PrepareScript.Nothing
        MSBuildProps = Map.empty
        MainSolution = "Argu.sln"
        SDKRequirementsDescription = "6.0.402 with minor upgrades"
    }

let fcs_20240127 =
    {
        Name = "FCS"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("dotnet", "fsharp", "9ae94bb9f96f07a416777852537bd0310e4764ab")
        PrepareScript = PrepareScript.Nothing
        MSBuildProps = Map.empty |> Map.add "BUILDING_USING_DOTNET" "true"
        MainSolution = "FSharp.Compiler.Service.sln"
        SDKRequirementsDescription = ""
    }
    
let giraffe =
    {
        Name = "Giraffe"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("giraffe-fsharp", "Giraffe", "6a364955f77609e9265c577ddd817c92fefe104c")
        PrepareScript = PrepareScript.Nothing
        MSBuildProps = Map.empty |> Map.add "TargetFramework" "net7.0" |> Map.add "TargetFrameworks" "net7.0"
        MainSolution = "Giraffe.sln"
        SDKRequirementsDescription = ""
    }
     
let fsharpDataPsBuildScript =
    "if ($IsWindows){ ./build.cmd -t Build; } Else { bash ./build.sh -t Build; }"
     
let fsharpData =
    {
        Name = "FSharpData"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "FSharp.Data", "8a6688f34abede0a80306e6c802601ef74edf473")
        PrepareScript = PrepareScript.Nothing // PrepareScript.PowerShell fsharpDataPsBuildScript
        MSBuildProps = Map.empty
        MainSolution = "FSharp.Data.sln"
        SDKRequirementsDescription = ""
    }
    
let fake =
    {
        Name = "FAKE"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "FAKE", "3f98e8d3ee4ae10aa127f2712222671f20f9367e")
        PrepareScript = PrepareScript.Nothing
        MSBuildProps = Map.empty
        MainSolution = "FAKE.sln"
        SDKRequirementsDescription = "6.0.101 with latestMinor upgrades"
    }
    
let paket =
    {
        Name = "Paket"
        Sample.CodebaseSpec = CodebaseSpec.MakeGithub ("fsprojects", "Paket", "4954f9f2fc6f6494edf54674bf926d22c80fab49")
        PrepareScript = PrepareScript.Nothing
        MSBuildProps = Map.empty
        MainSolution = "Paket.sln"
        SDKRequirementsDescription = "8.0.101 with latestMinor upgrades"
    }
        
let all =
    [
        fantomas
        argu
        fcs_20240127
        giraffe
        fsharpData
        fake
        paket
    ]
