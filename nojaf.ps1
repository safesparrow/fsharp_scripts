# Courtesy of https://blog.nojaf.com/2023/02/02/my-fsharp-compiler-scripts/

function Sync-Main(){
    git checkout main
    git fetch upstream
    git rebase upstream/main
    git push
}

function Format-Changed(){
    $root = git rev-parse --show-toplevel
    Push-Location $root
    $files = git status --porcelain | Where-Object { ($_.StartsWith(" M") -or $_.StartsWith("AM")) -and (Test-FSharpExtension $_) } | ForEach-Object { $_.substring(3) }
    & "dotnet" "fantomas" $files
    Pop-Location
}

function Surface-Area() {
    $env:TEST_UPDATE_BSL=1 
    dotnet test tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj --filter "SurfaceAreaTest"
    dotnet test tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj --filter "SurfaceAreaTest"
    dotnet test tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj --filter "SurfaceAreaTest" -c Release
    dotnet test tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj --filter "SurfaceAreaTest" -c Release
}

function ReadyToRun() {
    & dotnet publish .\src\fsc\fscProject\fsc.fsproj -c Release -r win-x64 -p:PublishReadyToRun=true -f net7.0 --no-self-contained
}

function Get-Mvid($dll) {
    & dotnet fsi (Join-Path $PSScriptRoot "mvid-reader.fsx") $dll
}
