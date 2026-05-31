// Native AOT smoke test. Native-compiled (PublishAot) and run by CI; exercises the parsing paths
// (System.Text.Json JsonDocument, DateTimeOffset.Parse, string splitting) and process plumbing of all
// three libraries under AOT, without needing the real git/jj/gh binaries. Drives the public command
// methods through the internal ICommandExecutor seam (visible via InternalsVisibleTo) with canned output.
// Exits 0 and prints "AOT smoke OK" on success; non-zero on any mismatch.

using Vcs.Git;
using Vcs.GitHub;
using Vcs.Jujutsu;

const char US = ''; // unit separator used in the git/jj log templates
const char RS = ''; // record separator

var failures = 0;

void Check(bool condition, string label)
{
	if (condition)
	{
		Console.WriteLine($"  ok: {label}");
	}
	else
	{
		Console.Error.WriteLine($"  FAIL: {label}");
		failures++;
	}
}

Console.WriteLine("Vcs.Git:");
var status = await new GitCli(new GitFake(new GitCommandResult("M  staged.cs\n?? café-naïve-日本語.txt", string.Empty, 0)))
	.StatusAsync();
Check(status.Count == 2, "status parses two entries");
Check(status.Count == 2 && status[1].Path == "café-naïve-日本語.txt", "status keeps non-ASCII path verbatim");

var logOut = $"h1{US}s1{US}Alice{US}2026-05-30T10:00:00+00:00{US}First subject{RS}";
var commits = await new GitCli(new GitFake(new GitCommandResult(logOut, string.Empty, 0))).LogAsync();
Check(commits.Count == 1 && commits[0].Author == "Alice", "log parses author");
Check(commits.Count == 1 && commits[0].Date == new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero),
	"log parses ISO-8601 date (DateTimeOffset.Parse under invariant globalization)");

var branches = await new GitCli(new GitFake(new GitCommandResult("* main\n  feature", string.Empty, 0))).BranchesAsync();
Check(branches.Count == 2 && branches[0] is { Name: "main", IsCurrent: true }, "branches mark current");

Console.WriteLine("Vcs.Jujutsu:");
var jjOut = $"chg1{US}commit1{US}false{US}First change{RS}chg2{US}commit2{US}true{US}{RS}";
var changes = await new JujutsuCli(new JjFake(new JjCommandResult(jjOut, string.Empty, 0))).LogAsync();
Check(changes.Count == 2 && changes[0] == new JjChange("chg1", "commit1", "First change", false), "jj log parses change");
Check(changes.Count == 2 && changes[1].Empty && changes[1].Description.Length == 0, "jj log tolerates empty description");

Console.WriteLine("Vcs.GitHub:");
const string prJson = """
	[ { "number": 12, "title": "Add feature", "state": "OPEN", "headRefName": "feature", "baseRefName": "main", "url": "https://x/12" } ]
	""";
var prs = await new GitHubCli(new GitHubFake(new GitHubCommandResult(prJson, string.Empty, 0))).PrListAsync();
Check(prs.Count == 1 && prs[0] == new GitHubPullRequest(12, "Add feature", "OPEN", "feature", "main", "https://x/12"),
	"pr list parses JSON array (System.Text.Json JsonDocument under AOT)");

const string repoJson = """
	{ "name": "r", "owner": { "login": "o" }, "description": null, "url": "u", "isPrivate": true, "defaultBranchRef": null }
	""";
var repo = await new GitHubCli(new GitHubFake(new GitHubCommandResult(repoJson, string.Empty, 0))).RepoViewAsync();
Check(repo is { Name: "r", Owner: "o", Description: null, IsPrivate: true, DefaultBranch: "" }, "repo view handles null fields");

// Best-effort: prove a real child process can be spawned under AOT when `git` is present.
// Never fails the run — the deterministic checks above are the contract.
try
{
	var version = await new GitCli().VersionAsync();
	Console.WriteLine($"  ok: spawned real `git` under AOT ({version})");
}
catch (Exception ex)
{
	Console.WriteLine($"  (skipped real-process check: {ex.GetType().Name})");
}

Console.WriteLine(failures == 0 ? "AOT smoke OK" : $"AOT smoke FAILED ({failures} check(s))");
return failures == 0 ? 0 : 1;

sealed class GitFake(GitCommandResult result) : Vcs.Git.ICommandExecutor
{
	public Task<GitCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
		=> Task.FromResult(result);
}

sealed class JjFake(JjCommandResult result) : Vcs.Jujutsu.ICommandExecutor
{
	public Task<JjCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
		=> Task.FromResult(result);
}

sealed class GitHubFake(GitHubCommandResult result) : Vcs.GitHub.ICommandExecutor
{
	public Task<GitHubCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
		=> Task.FromResult(result);
}
