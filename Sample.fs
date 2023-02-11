module Scripts.Sample

open Scripts.Git

type CodebaseSpec =
    | GitHub of CheckoutSpec
    | Local of string

type Sample =
    {
        CodebaseSpec : CodebaseSpec
        
    }