# Vcs

[![CI](https://github.com/ZelAnton/vcs-toolkit-dotNet/actions/workflows/ci.yml/badge.svg)](https://github.com/ZelAnton/vcs-toolkit-dotNet/actions/workflows/ci.yml)
[![CodeQL](https://github.com/ZelAnton/vcs-toolkit-dotNet/actions/workflows/codeql.yml/badge.svg)](https://github.com/ZelAnton/vcs-toolkit-dotNet/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A .NET toolkit for automating **Git**, **Jujutsu**, and **GitHub** by driving their
command-line tools (`git`, `jj`, `gh`) as child processes — typed, `async`, and
testable, with no native bindings and no reimplementation of the tools themselves.

Each tool is a separate library that builds to its own assembly and ships as its
own NuGet package, so you depend only on the ones you need.

| Package | Namespace | Automates | NuGet |
|---|---|---|---|
| [`Vcs.Git`](https://www.nuget.org/packages/Vcs.Git) | `Vcs.Git` | the `git` CLI | [![NuGet](https://img.shields.io/nuget/v/Vcs.Git.svg)](https://www.nuget.org/packages/Vcs.Git) |
| [`Vcs.Jujutsu`](https://www.nuget.org/packages/Vcs.Jujutsu) | `Vcs.Jujutsu` | the `jj` (Jujutsu) CLI | [![NuGet](https://img.shields.io/nuget/v/Vcs.Jujutsu.svg)](https://www.nuget.org/packages/Vcs.Jujutsu) |
| [`Vcs.GitHub`](https://www.nuget.org/packages/Vcs.GitHub) | `Vcs.GitHub` | the `gh` (GitHub) CLI | [![NuGet](https://img.shields.io/nuget/v/Vcs.GitHub.svg)](https://www.nuget.org/packages/Vcs.GitHub) |

## Why this toolkit

- **Typed where it counts, raw where it doesn't.** Common commands return parsed
  models (`GitCommit`, `GitStatusEntry`, `GitHubPullRequest`, `JjChange`, …); a raw
  escape hatch covers everything else, so you are never blocked by a missing wrapper.
- **Predictable error handling.** `RunAsync` throws a tool-specific exception
  (`GitCliException` / `JujutsuCliException` / `GitHubCliException`) carrying the exit
  code, stderr, and arguments. `RunRawAsync` never throws on a non-zero exit — it hands
  back the full result for you to inspect.
- **No surprises from the shell.** Arguments are passed through `ProcessStartInfo.ArgumentList`
  (no shell, no string-splitting), so spaces and metacharacters in arguments are safe.
- **Cancellation and timeouts** are first-class on every call.
- **Testable by design.** Each client implements an interface (`IGitCli` / `IJujutsuCli` /
  `IGitHubCli`) you can mock — no real process is started in your unit tests.
- **Independent packages.** The three libraries never reference one another; install only
  what you use.

## Requirements

- .NET 10.0 or later
- The CLI tools you intend to drive available on `PATH` (`git`, `jj`, `gh`), or an
  explicit path passed via the `executable` constructor argument
- `Vcs.GitHub` additionally needs an authenticated `gh` session (`gh auth login`)

## Installation

```sh
dotnet add package Vcs.Git
dotnet add package Vcs.Jujutsu
dotnet add package Vcs.GitHub
```

## Quick start

Each package exposes one client type with `async` command methods plus a raw
escape hatch. Process execution is handled by
[ProcessKit](https://www.nuget.org/packages/ProcessKit).

### `Vcs.Git`

```csharp
using Vcs.Git;

var git = new GitCli(workingDirectory: "/path/to/repo");

string version = await git.VersionAsync();          // "git version 2.45.0"
string branch  = await git.CurrentBranchAsync();    // "main"

foreach (var entry in await git.StatusAsync())
    Console.WriteLine($"{entry.Index}{entry.WorkTree} {entry.Path}");

await git.StageAsync(["src/Program.cs"]);
string hash = await git.CommitAsync("Add program", all: false);

foreach (var commit in await git.LogAsync(maxCount: 10))
    Console.WriteLine($"{commit.ShortHash} {commit.Date:d} {commit.Subject}");

// Escape hatch for anything not yet modelled — RunRawAsync never throws:
var result = await git.RunRawAsync(["remote", "-v"]);
if (result.IsSuccess)
    Console.WriteLine(result.StdOut);
```

### `Vcs.Jujutsu`

```csharp
using Vcs.Jujutsu;

var jj = new JujutsuCli(workingDirectory: "/path/to/repo");

await jj.DescribeAsync("Concise summary");          // jj describe -m ...
await jj.NewAsync("Next change");                   // jj new -m ...

foreach (var change in await jj.LogAsync(limit: 10))
    Console.WriteLine($"{change.ChangeId[..8]} {(change.Empty ? "(empty) " : "")}{change.Description}");

await jj.GitFetchAsync();
await jj.BookmarkSetAsync("main", revision: "@-");
await jj.GitPushAsync("main");
```

### `Vcs.GitHub`

```csharp
using Vcs.GitHub;

var gh = new GitHubCli(workingDirectory: "/path/to/repo");

if (!await gh.IsAuthenticatedAsync())
    throw new InvalidOperationException("Run `gh auth login` first.");

var repo = await gh.RepoViewAsync();                 // gh repo view --json ...
Console.WriteLine($"{repo.Owner}/{repo.Name} (default: {repo.DefaultBranch})");

foreach (var pr in await gh.PrListAsync(state: "open"))
    Console.WriteLine($"#{pr.Number} {pr.Title} [{pr.State}]");

string url = await gh.PrCreateAsync("My title", "My body", baseBranch: "main");

// Raw API escape hatch:
string json = await gh.ApiAsync("repos/OWNER/REPO/labels");
```

## Command surface

Every client also has the shared core — `VersionAsync`, the `RunAsync` / `RunRawAsync`
escape hatches, and the `Executable` / `WorkingDirectory` / `DefaultTimeout` /
`Environment` properties — on top of the typed commands below.

| `Vcs.Git` (`GitCli`) | Drives | Returns |
|---|---|---|
| `InitAsync(bare)` | `git init [--bare]` | — |
| `StatusAsync()` | `git status --porcelain=v1` | `IReadOnlyList<GitStatusEntry>` |
| `StageAsync(paths)` | `git add -- <paths>` | — |
| `CommitAsync(message, all)` | `git commit -m … [-a]` | new commit hash |
| `LogAsync(maxCount)` | `git log` | `IReadOnlyList<GitCommit>` |
| `CurrentBranchAsync()` | `git branch --show-current` | branch name (empty if detached) |
| `BranchesAsync()` | `git branch` | `IReadOnlyList<GitBranch>` |
| `RevParseAsync(revision)` | `git rev-parse <rev>` | full commit hash |
| `CreateBranchAsync(name)` | `git branch <name>` | — |
| `CheckoutAsync(reference)` | `git checkout <ref>` | — |

| `Vcs.Jujutsu` (`JujutsuCli`) | Drives | Returns |
|---|---|---|
| `StatusAsync()` | `jj status` | raw status text |
| `DescribeAsync(message, revision)` | `jj describe -r … -m …` | — |
| `NewAsync(message, parents)` | `jj new [parents] [-m …]` | — |
| `LogAsync(limit, revset)` | `jj log --no-graph -T …` | `IReadOnlyList<JjChange>` |
| `BookmarkListAsync()` | `jj bookmark list` | raw list text |
| `BookmarkSetAsync(name, revision)` | `jj bookmark set <name> -r …` | — |
| `GitFetchAsync()` | `jj git fetch` | — |
| `GitPushAsync(bookmark)` | `jj git push [--bookmark …]` | — |

| `Vcs.GitHub` (`GitHubCli`) | Drives | Returns |
|---|---|---|
| `IsAuthenticatedAsync()` | `gh auth status` | `bool` |
| `RepoViewAsync(repository)` | `gh repo view --json …` | `GitHubRepository` |
| `PrListAsync(state, limit)` | `gh pr list --json …` | `IReadOnlyList<GitHubPullRequest>` |
| `PrViewAsync(number)` | `gh pr view <n> --json …` | `GitHubPullRequest` |
| `PrCreateAsync(title, body, baseBranch)` | `gh pr create …` | new PR URL |
| `ApiAsync(endpoint, method)` | `gh api <endpoint> [-X …]` | raw response body |

## Behaviour & conventions

These rules are shared by all three clients.

### `RunAsync` vs `RunRawAsync`

- **`RunAsync`** is the strict path: it **throws** the tool-specific exception on a
  non-zero exit and returns the **trimmed** stdout (`string`) on success. Every typed
  command is built on it.
- **`RunRawAsync`** is the lenient path: it **never throws** on a non-zero exit and
  returns a `*CommandResult` (`StdOut`, `StdErr`, `ExitCode`, `IsSuccess`, `WasTimedOut`)
  with stdout **untrimmed** — use it when you need the exact bytes or want to handle a
  failing exit code yourself.

### Timeouts

Pass a `defaultTimeout` to the constructor to kill any command that runs too long, or
override it per call via the `TimeSpan` overloads. On a timeout, `RunAsync` throws with
`TimedOut == true` and `RunRawAsync` returns a result with `WasTimedOut == true`. A
non-positive timeout is rejected with `ArgumentOutOfRangeException`.

```csharp
var git = new GitCli(workingDirectory: ".", defaultTimeout: TimeSpan.FromSeconds(30));
var slowFetch = await git.RunRawAsync(["fetch", "--all"], TimeSpan.FromMinutes(5));
```

### Environment & standard input

Pass extra environment variables for every command via the `environment` constructor
argument (it is snapshotted on construction and exposed as the `Environment` property,
so later mutation of your dictionary cannot change the client). Pipe stdin to a single
command via the `standardInput` overloads:

```csharp
var env = new Dictionary<string, string> { ["GIT_AUTHOR_NAME"] = "CI" };
var git = new GitCli(workingDirectory: ".", environment: env);

// `standardInput` is piped to the process's stdin:
string sha = await git.RunAsync(["hash-object", "--stdin"], standardInput: "blob contents\n");
```

### Error handling

| Situation | `RunAsync` | `RunRawAsync` |
|---|---|---|
| Non-zero exit | throws `*CliException` (`ExitCode`, `StdErr`, `Arguments`) | returns result with `IsSuccess == false` |
| Timed out | throws `*CliException` with `TimedOut == true` | returns result with `WasTimedOut == true` |
| Executable missing / not startable | throws `*CliException` (`Could not start …`, inner `Win32Exception`) | same |
| Malformed tool output (`Vcs.GitHub` JSON) | throws `GitHubCliException` (inner `JsonException`) | n/a (no parsing) |

### Encoding & paths

Output is decoded as UTF-8. `Vcs.Git.StatusAsync` runs with `core.quotePath=false`, so
non-ASCII paths come back verbatim rather than C-quoted/octal-escaped.

### Thread-safety & reuse

A client holds no per-call mutable state, so a single instance is safe to reuse across
concurrent calls. Each call spawns its own child process.

## Dependency injection & mocking

Each client implements an interface exposing its full command surface —
`IGitCli` (`GitCli`), `IJujutsuCli` (`JujutsuCli`), `IGitHubCli` (`GitHubCli`).
Depend on the interface so call sites stay testable: register the concrete type
for DI and substitute a mock in unit tests with any mocking framework.

```csharp
// production wiring
services.AddSingleton<IGitCli>(_ => new GitCli(workingDirectory: repoPath));

// in a test (NSubstitute) — no real `git` process is started
var git = Substitute.For<IGitCli>();
git.CurrentBranchAsync().Returns("main");
git.LogAsync(maxCount: 1).Returns([new GitCommit("abc", "abc", "Ann", DateTimeOffset.UtcNow, "Init")]);
```

The exception types also have public constructors, so a mocked client can be made to
throw a realistic failure:

```csharp
git.CommitAsync(Arg.Any<string>())
   .Throws(new GitCliException("nothing to commit", exitCode: 1, stdErr: "nothing to commit, working tree clean"));
```

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

The default test run is fully hermetic — it uses a fake process executor and never
touches a real binary. Tests that drive the real `git` / `jj` / `gh` tools are marked
`[Explicit]` and skipped by default; run them by name filter, e.g.:

```sh
dotnet test Vcs.slnx --filter "FullyQualifiedName~IntegrationTests"
```

To exercise the Linux/Unix path from Windows, see
[docs/linux-testing.md](docs/linux-testing.md) (`pwsh scripts/test-linux.ps1`).

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
