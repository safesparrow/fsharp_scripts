This F# project is a code sample used to reproduce an issue around non-deterministic compilation behaviour when using graph-based type-checking (`--test:GraphBasedChecking`).

Depending on which of these two files: `NonGenericUse.fs` and `GenericUse.fs` is type-checked first, the name of a certain typar inside the `A.ReportWarnings` method will be different.