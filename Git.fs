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
        Log.Information ("Checkout revision {revision} in {path}", revision, repo.Info.Path)
        Commands.Checkout (repo, revision) |> ignore
    else
        use repo = new Repository(dir)
        let tipSha = repo.Head.Tip.Sha
        if tipSha <> revision then
            failwith $"Local repository canonical name expected to be '{revision}' but was '{tipSha}'."
        if repo.RetrieveStatus().IsDirty then
            failwith $"Local repository is dirty - cannot proceed."
        Log.Information $"{revision} already checked out in {dir}"

/// Removes all untracked files and 
let fullClean (repo : Repository) =
    repo.Reset(ResetMode.Hard)
    repo.RemoveUntrackedFiles()
    let untracked = repo.RetrieveStatus().Untracked
    untracked
    |> Seq.iter (fun f ->
        if Directory.Exists(f.FilePath) then
            Directory.Delete(f.FilePath)
        else
            File.Delete(f.FilePath)
    )

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
    static member Make(org : string, repo : string) =
        {
            Org = org
            Repo = repo
        }

type CheckoutSpec =
    {
        OrgRepo : OrgRepo
        Revision : string
    }
    /// Not fully reliable, but helps avoid long path issues.
    member this.RevisionShort = this.Revision.Substring(0, 8)
    member this.Name = $"{this.OrgRepo.Name}__{this.RevisionShort}"
    static member Make(org : string, repo : string, rev : string) =
        {
            OrgRepo = OrgRepo.Make(org, repo)
            Revision = rev
        }

type CheckoutsConfig =
    {
        CacheDir : string
    }

module CheckoutSpec =
    let subdir (spec : CheckoutSpec) =
        Path.Combine($"{spec.OrgRepo.Org}__{spec.OrgRepo.Repo}", spec.RevisionShort)
    
    let dir (config : CheckoutsConfig) (spec : CheckoutSpec) =
        Path.Combine(config.CacheDir, subdir spec)

let prepareCheckout (config : CheckoutsConfig) (spec : CheckoutSpec) =
    let dir = CheckoutSpec.dir config spec
    let repoUrl = spec.OrgRepo.GitUrl
    cloneIfDoesNotExist dir repoUrl spec.Revision