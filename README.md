# Vcs

A .NET toolkit for automating Git, Jujutsu, and GitHub through CLI process execution.

Each tool is a separate library that builds to its own assembly and ships as its
own NuGet package, so you depend only on the ones you need.

| Package | Namespace | Automates |
|---|---|---|
| [`Vcs.Git`](https://www.nuget.org/packages/Vcs.Git) | `Vcs.Git` | the `git` CLI |
| [`Vcs.Jujutsu`](https://www.nuget.org/packages/Vcs.Jujutsu) | `Vcs.Jujutsu` | the `jj` (Jujutsu) CLI |
| [`Vcs.GitHub`](https://www.nuget.org/packages/Vcs.GitHub) | `Vcs.GitHub` | the `gh` (GitHub) CLI |

## Requirements

- .NET 10.0 or later
- The CLI tools you intend to drive available on `PATH` (`git`, `jj`, `gh`)

## Installation

```sh
dotnet add package Vcs.Git
dotnet add package Vcs.Jujutsu
dotnet add package Vcs.GitHub
```

## Usage

TODO: document the public API of each package with usage examples. The current
types (`GitCli`, `JujutsuCli`, `GitHubCli`) are placeholders.

## Repository layout

```
src/
  Vcs.Git/        Vcs.Jujutsu/        Vcs.GitHub/        # one library per tool
tests/
  Vcs.Git.Tests/  Vcs.Jujutsu.Tests/  Vcs.GitHub.Tests/  # NUnit tests, one per library
Vcs.slnx                                                 # solution (build ordering)
```

## Build and test

```sh
dotnet build Vcs.slnx
dotnet test  Vcs.slnx
```

Cross-project references use `Reference` + `AssemblySearchPaths` (not
`ProjectReference`); build ordering is declared via `BuildDependency` entries in
`Vcs.slnx`. See [CLAUDE.md](CLAUDE.md) and [AGENTS.md](AGENTS.md) for the full
conventions.

## Verifying packages

Each GitHub Release ships a `SHA256SUMS` file alongside the `.nupkg` / `.snupkg`.
Download the files into the same directory, then:

```sh
sha256sum -c SHA256SUMS
```

Packages on NuGet.org also carry a repository signature from nuget.org, which you
can inspect with `dotnet nuget verify Vcs.Git.<version>.nupkg --all`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the version history.

## License

This project is licensed under the [MIT License](LICENSE).
