cd C:\projekty\fsharp\jetbrains-fsharp
dotnet build .\FSharp.Compiler.Service.sln
cp -Path "C:\projekty\fsharp\jetbrains-fsharp\artifacts\bin\FSharp.Compiler.Service\Debug\netstandard2.0\FSharp.Compiler.Service.xml","C:\projekty\fsharp\jetbrains-fsharp\artifacts\bin\FSharp.Compiler.Service\Debug\netstandard2.0\FSharp.Compiler.Service.dll" -Destination c:\Users\janus\.nuget\packages\jetbrains.fsharp.compiler.service\2023.1.2\lib\netstandard2.0\ ; cd C:\projekty\fsharp\resharper-fsharp\ReSharper.FSharp\
cd C:\projekty\fsharp\resharper-fsharp\ReSharper.FSharp
dotnet clean -v:m
dotnet restore --force -v:m
dotnet build -v:m /p:NoWarn=MSB3277
cd ..\rider-fsharp\
./gradlew runIde