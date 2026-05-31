# Changelog

All notable changes to the **Vcs** packages (`Vcs.Git`, `Vcs.Jujutsu`,
`Vcs.GitHub`) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial `Vcs.Git`, `Vcs.Jujutsu`, and `Vcs.GitHub` packages with placeholder CLI client types.
- `Vcs.Git`: async `GitCli` over the `git` CLI — `VersionAsync`, `InitAsync`, `StatusAsync`, `StageAsync`, `CommitAsync`, `LogAsync`, `CurrentBranchAsync`, `CreateBranchAsync`, `CheckoutAsync` — plus a raw escape hatch (`RunAsync`/`RunRawAsync`), `GitCommandResult`, `GitCommit`/`GitStatusEntry` models, and `GitCliException`. Process execution is backed by ProcessKit.
- `Vcs.Jujutsu`: async `JujutsuCli` over the `jj` CLI — `VersionAsync`, `StatusAsync`, `DescribeAsync`, `NewAsync`, `LogAsync`, `BookmarkListAsync`, `BookmarkSetAsync`, `GitFetchAsync`, `GitPushAsync` — plus a raw escape hatch (`RunAsync`/`RunRawAsync`), `JjCommandResult`, the `JjChange` model, and `JujutsuCliException`. Process execution is backed by ProcessKit.
- `Vcs.GitHub`: async `GitHubCli` over the `gh` CLI — `VersionAsync`, `IsAuthenticatedAsync`, `RepoViewAsync`, `PrListAsync`, `PrViewAsync`, `PrCreateAsync`, `ApiAsync` — plus a raw escape hatch (`RunAsync`/`RunRawAsync`), `GitHubCommandResult`, `GitHubPullRequest`/`GitHubRepository` models, and `GitHubCliException`. JSON output is parsed with `System.Text.Json`; process execution is backed by ProcessKit.
- All three clients: configurable command timeouts — a `defaultTimeout` constructor argument plus `TimeSpan` overloads of `RunAsync`/`RunRawAsync` for per-call overrides. On a timeout the command is killed; `RunAsync` throws with `*CliException.TimedOut == true` and `RunRawAsync` reports `*CommandResult.WasTimedOut == true`.
- All three packages: public `IGitCli` / `IJujutsuCli` / `IGitHubCli` interfaces (implemented by `GitCli` / `JujutsuCli` / `GitHubCli`) exposing the full command surface, so consumers can depend on the interface for DI and substitute a mock/fake in tests (e.g. `Substitute.For<IGitCli>()`).
- All three packages: public constructors on `GitCliException` / `JujutsuCliException` / `GitHubCliException` (`(string message, int exitCode = 0, string stdErr = "", string arguments = "", bool timedOut = false)`) so consumers can construct them in tests — e.g. to make a mocked client throw.
- All three packages: an `environment` constructor argument (snapshotted and exposed as the `Environment` property) that applies extra environment variables to every command, plus `standardInput` overloads of `RunAsync`/`RunRawAsync` that pipe a string to the process's stdin.
- `Vcs.Git`: `BranchesAsync` returning `GitBranch` values (with `IsCurrent`) and `RevParseAsync` to resolve a revision to its full hash.

### Changed
- All three clients: a missing or unstartable executable now throws the library's own `*CliException` (`Could not start ...`) instead of leaking a raw `System.ComponentModel.Win32Exception`; the original is preserved as `InnerException`.
- `Vcs.Git`: replaced the placeholder `GitCli.ExecutableName` property with the real command API (`GitCli.Executable`).
- `Vcs.Jujutsu`: replaced the placeholder `JujutsuCli.ExecutableName` property with the real command API (`JujutsuCli.Executable`).
- `Vcs.GitHub`: replaced the placeholder `GitHubCli.ExecutableName` property with the real command API (`GitHubCli.Executable`).

### Fixed
- `Vcs.Git`: `StatusAsync` now returns non-ASCII paths verbatim (runs `git -c core.quotePath=false status`); previously such paths came back C-quoted and octal-escaped (e.g. `"caf\303\251.txt"`).
- `Vcs.GitHub`: malformed or unexpected `gh --json` output now throws `GitHubCliException` (with the original `JsonException` as `InnerException`) instead of leaking a raw `System.Text.Json.JsonException`.

[Unreleased]: https://github.com/ZelAnton/vcs-toolkit-dotNet/commits/main
