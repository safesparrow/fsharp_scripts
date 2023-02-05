$CacheDir = Resolve-Path $env:FSHARP_TESTS_CACHE_DIR
$ErrorActionPreference = "Stop"

if (!$CacheDir) {
    throw "FSHARP_TESTS_CACHE_DIR must be set to a directory for caching git checkouts"
}

function Log($Message){
    Write-Host $Message
}
function AssertSuccessful($name) {
    if($LastExitCode -ne 0){
        throw "'$name' failed with exit code $LastExitCode"
    }
}

class CheckoutSpec
{
    [string]$Org
    [string]$Repo
    [string]$Revision
}

function Checkout-Dir(
    [Parameter(mandatory)][CheckoutSpec]$Spec
){
    $c = ("$($Spec.Org)__$($Spec.Repo)")
    $a = (Join-Path $CacheDir $c)
    $b = $($Spec.Revision).Substring(0, 8)
    return Join-Path $a $b
}

function Checkout(
    [Parameter(mandatory)][CheckoutSpec]$Spec
    ){
    $dir = Checkout-Dir $Spec
    $repoUrl = "https://github.com/$($Spec.Org)/$($Spec.Repo)"
    
    if(Test-Path -Path $dir){
        Log "Repo dir '$dir' already exists"
    } else {
        New-Item -ItemType Directory -Path $dir -Force
        git -C $dir clone $repoUrl .; AssertSuccessful "git"
        git -C $dir checkout ${Spec.Revision}; AssertSuccessful "git"
    }
}

function PrepareFsc(
    [Parameter(mandatory)][string]$Dir,
    [bool]$Release = $false
    ){
    $configuration = "Debug"
    if($Release){
        $configuration = "Release"
    }
    if($IsWindows){
        $path = Join-Path $Dir "eng/Build.ps1"
        & $path -restore -build -noVisualStudio -c $configuration
    } else {
        throw "Non-Windows OS not supported"
    }
}

$c = [CheckoutSpec]@{
    Org = "dotnet"
    Repo = "fsharp"
    Revision = "7aa5f480fe78d0194ec3e19c3099568b63f01760"
}

class Sample
{
    [CheckoutSpec]$Checkout
    [string]$PrepareScript
    [string]$Solution
}

$f = [CheckoutSpec]@{
    Org = "fsprojects"
    Repo = "fantomas"
    Revision = "18f31541e983c9301e6a55ba6582817bc704cb6f"
}
$s = [Sample]@{
    Checkout = $f
    PrepareScript = ""
    Solution = "fantomas.sln"
}

function SetupTest(
    [Sample]$Sample
    ){
    $dir = Checkout-Dir -Spec $f
    Push-Location -Path $dir
    try {
        Checkout -Spec $f
        if($Sample.PrepareScript){
            Invoke-Expression $Sample.PrepareScript
        }
        $projects = dotnet sln $Sample.Solution list
        echo $projects
    }
    finally {
        Pop-Location
    }
}

function Create-ArgsFile($project, $argsFile) {
    & dotnet fsi (Join-Path $PSScriptRoot "args-file.fsx") $project $argsFile
}

function Fsc([string]$argsPath){
    dotnet "C:\projekty\fsharp\fsharp_main\artifacts\bin\fsc\Release\net7.0\win-x64\publish\fsc.exe" "@$argsPath" --times
}

SetupTest $s
$p = "C:\projekty\fsharp\.cache\fsprojects__fantomas\18f31541\src\Fantomas.Client\Fantomas.Client.fsproj"
Create-ArgsFile $p "fsc.args"
echo $(Get-Item "fsc.args")