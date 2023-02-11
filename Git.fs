module Scripts.Git

open System.IO
open LibGit2Sharp
open Serilog

let private clone (dir : string) (gitUrl : string) : Repository =
    if Directory.Exists dir then
        failwith $"{dir} already exists for code root"

    Log.Verbose ("Fetching '{gitUrl}' in '{dir}'", gitUrl, dir)
    Repository.Init dir |> ignore
    let repo = new Repository (dir)
    let remote = repo.Network.Remotes.Add ("origin", gitUrl)
    repo.Network.Fetch (remote.Name, [])
    repo

let private cloneIfDoesNotExist (dir : string) (repoUrl : string) (revision : string) =
    if Repository.IsValid dir |> not then
        Log.Information $"Checking out revision {revision} in {dir}"
        use repo = clone dir repoUrl
        Log.Information ("Checkout revision {revision} in {repo.Info.Path}", revision, repo.Info.Path)
        Commands.Checkout (repo, revision) |> ignore
    else
        use repo = new Repository(dir)
        let canonicalName = repo.Head.Reference.CanonicalName
        if canonicalName <> revision then
            failwith $"Local repository canonical name expected to be '{revision}' but was '{canonicalName}'."
        if repo.RetrieveStatus().IsDirty then
            failwith $"Local repository is dirty - cannot proceed."
        Log.Information $"{revision} already checked out in {dir}"

/// Eg. "dotnet/fsharp"
type OrgRepo =
    {
        /// GitHub org, eg. "dotnet" for "dotnet/fsharp"
        Org : string
        /// GitHub repo, eg. "fsharp" for "dotnet/fsharp"
        Repo : string
    }
    member this.Name = $"{this.Org}/{this.Repo}"
    member this.GitUrl = $"https://github.com/{this.Name}"
    override this.ToString() = this.Name

type CheckoutSpec =
    {
        OrgRepo : OrgRepo
        Revision : string
    }
    /// Not fully reliable, but helps avoid long path issues.
    member this.RevisionShort = this.Revision.Substring(0, 8)
    member this.Name = $"{this.OrgRepo.Name}__{this.RevisionShort}"

type CheckoutsConfig =
    {
        CacheDir : string
    }

module CheckoutSpec =
    let subdir (spec : CheckoutSpec) =
        Path.Combine(spec.OrgRepo.Name, spec.RevisionShort)
    
    let dir (config : CheckoutsConfig) (spec : CheckoutSpec) =
        Path.Combine(config.CacheDir, subdir spec)

let prepareCheckout (config : CheckoutsConfig) (spec : CheckoutSpec) =
    let dir = CheckoutSpec.dir config spec
    let repoUrl = spec.OrgRepo.GitUrl
    cloneIfDoesNotExist dir repoUrl spec.Revision