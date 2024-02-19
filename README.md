# What is this?
A set of scripts/utility methods/apps for working with the F# compiler.

It's a Work In Progress.

# How to use it?
The code is not currently packaged in any way.
To use it, please clone the repository and browse its files.

# FSharp Compiler Service testing
To run an IDE-like test of the FSharp.Compiler.Service on one of open-source F# codebases, run:
```
cd Scripts.IDETests
dotnet run %sample_name% --tcmodes On # eg. dotnet run fantomas --tcmodes On
```

To list available samples, run:
```
dotnet run -- --help
```

The test checks out, restore, loads & type-checks a given solution and reports all the compiler diagnostics.

# Contributing
If you'd like to propose/make changes to the repository, please raise an issue or a Pull Request.
