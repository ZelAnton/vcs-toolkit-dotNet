# Changelog

All notable changes to the **Vcs** packages (`Vcs.Git`, `Vcs.Jujutsu`,
`Vcs.GitHub`) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial `Vcs.Git`, `Vcs.Jujutsu`, and `Vcs.GitHub` packages with placeholder CLI client types.
- `Vcs.Git`: async `GitCli` over the `git` CLI — `VersionAsync`, `InitAsync`, `StatusAsync`, `StageAsync`, `CommitAsync`, `LogAsync`, `CurrentBranchAsync`, `CreateBranchAsync`, `CheckoutAsync` — plus a raw escape hatch (`RunAsync`/`RunRawAsync`), `GitCommandResult`, `GitCommit`/`GitStatusEntry` models, and `GitCliException`. Process execution is backed by ProcessKit.

### Changed
- `Vcs.Git`: replaced the placeholder `GitCli.ExecutableName` property with the real command API (`GitCli.Executable`).

### Fixed
-

[Unreleased]: https://github.com/ZelAnton/vcs-toolkit-dotNet/commits/main
