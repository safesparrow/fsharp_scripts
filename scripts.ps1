$CacheDir = $env:FSHARP_TESTS_CACHE_DIR
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

function Checkout(
    [Parameter(mandatory)][string]$Org,
    [Parameter(mandatory)][string]$Repo,
    [Parameter(mandatory)][string]$Revision
    ){
    $dir = Join-Path (Join-Path $CacheDir ("${Org}__${Repo}")) $Revision
    $repoUrl = "https://github.com/$Org/$Repo"
    
    if(Test-Path -Path $dir){
        Log "Repo dir '$dir' already exists"
    } else {
        New-Item -ItemType Directory -Path $dir -Force
        git -C $dir init; AssertSuccessful "git"
        git -C $dir init; AssertSuccessful "git"
        git -C $dir remote add origin $repoUrl; AssertSuccessful "git"
    }
    git -C $dir fetch; AssertSuccessful "git"
    git -C $dir checkout $Revision; AssertSuccessful "git"
}
Checkout -Org dotnet -Repo fsharp -Revision 7aa5f480fe78d0194ec3e19c3099568b63f01760