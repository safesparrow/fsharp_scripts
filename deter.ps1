$env:FSHARP_EXPERIMENTAL_FEATURES=""

function go {
    param($n, $i, $name)

    cd C:\projekty\fsharp\fsharp_main\src\compiler

    $out = "c:/projekty/fsharp/fsharp_main/src/compiler/fsc_${n}_${i}.args"
    Get-Content "c:/projekty/fsharp/fsharp_main/src/compiler/fsc_${name}.args" -Head $n > $out

    $a = dotnet C:/projekty/fsharp/opt/artifacts/bin/fsc/Release/net7.0/win-x64/publish/fsc.dll @$out
    $newHash = Get-FileHash "c:/projekty/fsharp/fsharp_main/artifacts/obj/fsharp.compiler.service/Debug/netstandard2.0/fsharp.compiler.service.dll"

    Out-File -FilePath "hash_${n}_${i}_${name}.hash" -InputObject $newHash.Hash

    Write-Host "hash_${n}_${i}_${name}.hash = ${newHash.Hash}"

    return $newHash.Hash
}
$goDef = ${function:go}.ToString()

cd C:\projekty\fsharp\fsharp_main\src\compiler
$n = 5
$zeroHash = ""
$name = ""

function goGo {
    param($n, $name)
    # go -n $n -i 10 -name $name
    #$zeroHash = Get-Content "hash_${n}_0_deterministic.hash" # go -n $n -i 0
    foreach($i in 1..10) {
        $hash = go -n $n -i $i -name $name;
        #if($hash -ne $zeroHash){Write-Host "$hash != $zeroHash"}
    }
}
function goGoGo{
    param($n)
    go -n $n -i 0 -name "deterministic"
    # goGo -n $n -name "deterministic"
    goGo -n $n -name "nondeterministic"
}
goGoGo -n 429
#goGoGo -n 479