namespace Vcs.GitHub.Tests;

[TestFixture]
public class GitHubCliTests
{
	private sealed class FakeExecutor(params GitHubCommandResult[] results) : ICommandExecutor
	{
		private readonly Queue<GitHubCommandResult> _results = new(results);

		public List<IReadOnlyList<string>> Calls { get; } = [];

		public List<TimeSpan?> Timeouts { get; } = [];

		public List<string?> StandardInputs { get; } = [];

		public Task<GitHubCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
		{
			Calls.Add(arguments);
			Timeouts.Add(timeout);
			StandardInputs.Add(standardInput);
			var result = _results.Count > 0 ? _results.Dequeue() : new GitHubCommandResult(string.Empty, string.Empty, 0);
			return Task.FromResult(result);
		}
	}

	private static GitHubCommandResult Ok(string stdOut) => new(stdOut, string.Empty, 0);

	[Test]
	public void DefaultExecutable_IsGh()
	{
		Assert.That(new GitHubCli().Executable, Is.EqualTo("gh"));
	}

	[Test]
	public async Task VersionAsync_RunsVersionFlag_AndTrimsOutput()
	{
		var fake = new FakeExecutor(Ok("gh version 2.62.0 (2024-01-01)\n"));
		var gh = new GitHubCli(fake);

		var version = await gh.VersionAsync();

		Assert.That(version, Is.EqualTo("gh version 2.62.0 (2024-01-01)"));
		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "--version" }));
	}

	[Test]
	public void RunAsync_NonZeroExit_Throws()
	{
		var fake = new FakeExecutor(new GitHubCommandResult(string.Empty, "gh: not found", 1));
		var gh = new GitHubCli(fake);

		var ex = Assert.ThrowsAsync<GitHubCliException>(async () => await gh.RunAsync(["pr", "list"]));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.ExitCode, Is.EqualTo(1));
			Assert.That(ex.StdErr, Is.EqualTo("gh: not found"));
			Assert.That(ex.Arguments, Is.EqualTo("pr list"));
		});
	}

	[Test]
	public async Task RunRawAsync_NonZeroExit_DoesNotThrow()
	{
		var fake = new FakeExecutor(new GitHubCommandResult("out", "err", 4));
		var gh = new GitHubCli(fake);

		var result = await gh.RunRawAsync(["whatever"]);

		Assert.Multiple(() =>
		{
			Assert.That(result.IsSuccess, Is.False);
			Assert.That(result.ExitCode, Is.EqualTo(4));
		});
	}

	[Test]
	public async Task IsAuthenticatedAsync_ReflectsExitCode()
	{
		var authed = new GitHubCli(new FakeExecutor(Ok(string.Empty)));
		var notAuthed = new GitHubCli(new FakeExecutor(new GitHubCommandResult(string.Empty, "not logged in", 1)));

		Assert.That(await authed.IsAuthenticatedAsync(), Is.True);
		Assert.That(await notAuthed.IsAuthenticatedAsync(), Is.False);
	}

	[Test]
	public async Task PrListAsync_ParsesJson_AndPassesState()
	{
		const string json = """
			[
			  { "number": 12, "title": "Add feature", "state": "OPEN", "headRefName": "feature", "baseRefName": "main", "url": "https://github.com/o/r/pull/12" },
			  { "number": 9, "title": "Fix bug", "state": "MERGED", "headRefName": "bugfix", "baseRefName": "main", "url": "https://github.com/o/r/pull/9" }
			]
			""";
		var fake = new FakeExecutor(Ok(json));
		var gh = new GitHubCli(fake);

		var prs = await gh.PrListAsync(state: "all");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "pr", "list", "--json", "number,title,state,headRefName,baseRefName,url", "--state", "all" }));
		Assert.That(prs, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(prs[0], Is.EqualTo(new GitHubPullRequest(12, "Add feature", "OPEN", "feature", "main", "https://github.com/o/r/pull/12")));
			Assert.That(prs[1].Number, Is.EqualTo(9));
			Assert.That(prs[1].State, Is.EqualTo("MERGED"));
		});
	}

	[Test]
	public async Task PrListAsync_PassesLimit()
	{
		var fake = new FakeExecutor(Ok("[]"));
		var gh = new GitHubCli(fake);

		var prs = await gh.PrListAsync(limit: 200);

		Assert.That(prs, Is.Empty);
		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0], Does.Contain("--limit"));
			Assert.That(fake.Calls[0], Does.Contain("200"));
			Assert.That(fake.Calls[0], Does.Not.Contain("--state"));
		});
	}

	[Test]
	public async Task PrViewAsync_ParsesSinglePullRequest()
	{
		const string json = """
			{ "number": 7, "title": "Tidy up", "state": "OPEN", "headRefName": "tidy", "baseRefName": "develop", "url": "https://github.com/o/r/pull/7" }
			""";
		var fake = new FakeExecutor(Ok(json));
		var gh = new GitHubCli(fake);

		var pr = await gh.PrViewAsync(7);

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "pr", "view", "7", "--json", "number,title,state,headRefName,baseRefName,url" }));
		Assert.That(pr, Is.EqualTo(new GitHubPullRequest(7, "Tidy up", "OPEN", "tidy", "develop", "https://github.com/o/r/pull/7")));
	}

	[Test]
	public async Task RepoViewAsync_ParsesNestedFields()
	{
		const string json = """
			{
			  "name": "vcs-toolkit-dotNet",
			  "owner": { "login": "ZelAnton" },
			  "description": "A .NET toolkit",
			  "url": "https://github.com/ZelAnton/vcs-toolkit-dotNet",
			  "isPrivate": false,
			  "defaultBranchRef": { "name": "main" }
			}
			""";
		var fake = new FakeExecutor(Ok(json));
		var gh = new GitHubCli(fake);

		var repo = await gh.RepoViewAsync("ZelAnton/vcs-toolkit-dotNet");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "repo", "view", "ZelAnton/vcs-toolkit-dotNet", "--json", "name,owner,description,url,isPrivate,defaultBranchRef" }));
		Assert.That(repo, Is.EqualTo(new GitHubRepository(
			"vcs-toolkit-dotNet", "ZelAnton", "A .NET toolkit",
			"https://github.com/ZelAnton/vcs-toolkit-dotNet", false, "main")));
	}

	[Test]
	public async Task RepoViewAsync_NullDescription_BecomesNull()
	{
		const string json = """
			{ "name": "r", "owner": { "login": "o" }, "description": null, "url": "https://x", "isPrivate": true, "defaultBranchRef": null }
			""";
		var fake = new FakeExecutor(Ok(json));
		var gh = new GitHubCli(fake);

		var repo = await gh.RepoViewAsync();

		Assert.Multiple(() =>
		{
			Assert.That(repo.Description, Is.Null);
			Assert.That(repo.IsPrivate, Is.True);
			Assert.That(repo.DefaultBranch, Is.Empty);
		});
	}

	[Test]
	public async Task PrCreateAsync_BuildsArguments_AndReturnsUrl()
	{
		var fake = new FakeExecutor(Ok("https://github.com/o/r/pull/42\n"));
		var gh = new GitHubCli(fake);

		var url = await gh.PrCreateAsync("My title", "My body", baseBranch: "main");

		Assert.That(url, Is.EqualTo("https://github.com/o/r/pull/42"));
		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "pr", "create", "--title", "My title", "--body", "My body", "--base", "main" }));
	}

	[Test]
	public async Task ApiAsync_BuildsArgumentsWithMethod()
	{
		var fake = new FakeExecutor(Ok("{}"));
		var gh = new GitHubCli(fake);

		await gh.ApiAsync("repos/o/r/issues", method: "POST");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "api", "repos/o/r/issues", "-X", "POST" }));
	}

	[Test]
	public async Task DefaultTimeout_IsAppliedToCommands()
	{
		var fake = new FakeExecutor(Ok("gh version 2.62.0"));
		var gh = new GitHubCli(fake, defaultTimeout: TimeSpan.FromSeconds(11));

		await gh.VersionAsync();

		Assert.That(gh.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(11)));
		Assert.That(fake.Timeouts[0], Is.EqualTo(TimeSpan.FromSeconds(11)));
	}

	[Test]
	public async Task PerCallTimeout_OverridesDefault()
	{
		var fake = new FakeExecutor(Ok("{}"));
		var gh = new GitHubCli(fake, defaultTimeout: TimeSpan.FromSeconds(11));

		await gh.RunAsync(["api", "rate_limit"], TimeSpan.FromSeconds(4));

		Assert.That(fake.Timeouts[0], Is.EqualTo(TimeSpan.FromSeconds(4)));
	}

	[Test]
	public void RunAsync_Timeout_ThrowsWithTimedOutFlag()
	{
		var fake = new FakeExecutor(new GitHubCommandResult(string.Empty, string.Empty, -1) { WasTimedOut = true });
		var gh = new GitHubCli(fake);

		var ex = Assert.ThrowsAsync<GitHubCliException>(async () => await gh.RunAsync(["pr", "list"], TimeSpan.FromSeconds(1)));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.TimedOut, Is.True);
			Assert.That(ex.Message, Does.Contain("timed out"));
		});
	}

	[Test]
	public void Constructor_NonPositiveTimeout_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new GitHubCli(defaultTimeout: TimeSpan.Zero));
	}

	[Test]
	public void RunAsync_ExecutableNotFound_ThrowsCliException()
	{
		var gh = new GitHubCli(executable: "vcs-toolkit-no-such-binary-zzz");

		var ex = Assert.ThrowsAsync<GitHubCliException>(async () => await gh.RunAsync(["pr", "list"]));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.Message, Does.Contain("Could not start"));
			Assert.That(ex.InnerException, Is.InstanceOf<System.ComponentModel.Win32Exception>());
		});
	}

	[Test]
	public async Task PrListAsync_NonNumberNumberField_DefaultsToZero()
	{
		const string json = """
			[ { "number": null, "title": "t", "state": "OPEN", "headRefName": "h", "baseRefName": "b", "url": "u" } ]
			""";
		var gh = new GitHubCli(new FakeExecutor(Ok(json)));

		var prs = await gh.PrListAsync();

		Assert.That(prs[0].Number, Is.EqualTo(0));
	}

	[Test]
	public async Task RepoViewAsync_NoRepository_OmitsPositional()
	{
		const string json = """{ "name": "r", "owner": { "login": "o" }, "url": "u", "isPrivate": false, "defaultBranchRef": { "name": "main" } }""";
		var fake = new FakeExecutor(Ok(json));
		await new GitHubCli(fake).RepoViewAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "repo", "view", "--json", "name,owner,description,url,isPrivate,defaultBranchRef" }));
	}

	[Test]
	public async Task RepoViewAsync_EmptyDescription_BecomesNull()
	{
		const string json = """{ "name": "r", "owner": { "login": "o" }, "description": "", "url": "u", "isPrivate": false, "defaultBranchRef": { "name": "main" } }""";
		var repo = await new GitHubCli(new FakeExecutor(Ok(json))).RepoViewAsync();

		Assert.That(repo.Description, Is.Null);
	}

	[Test]
	public async Task PrListAsync_NoFilters_BuildsBaseArguments()
	{
		var fake = new FakeExecutor(Ok("[]"));
		await new GitHubCli(fake).PrListAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "pr", "list", "--json", "number,title,state,headRefName,baseRefName,url" }));
	}

	[Test]
	public async Task PrCreateAsync_NoBase_OmitsBaseFlag()
	{
		var fake = new FakeExecutor(Ok("https://github.com/o/r/pull/1\n"));
		await new GitHubCli(fake).PrCreateAsync("t", "b");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "pr", "create", "--title", "t", "--body", "b" }));
	}

	[Test]
	public async Task ApiAsync_NoMethod_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok("{}"));
		await new GitHubCli(fake).ApiAsync("rate_limit");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "api", "rate_limit" }));
	}

	[Test]
	public void WorkingDirectory_RoundTrips()
	{
		Assert.Multiple(() =>
		{
			Assert.That(new GitHubCli(workingDirectory: "/repo").WorkingDirectory, Is.EqualTo("/repo"));
			Assert.That(new GitHubCli().WorkingDirectory, Is.Null);
		});
	}

	[Test]
	public void Constructor_EmptyExecutable_Throws()
	{
		Assert.Throws<ArgumentException>(() => new GitHubCli(executable: string.Empty));
	}

	[Test]
	public void RunAsync_NullArguments_Throws()
	{
		var gh = new GitHubCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await gh.RunAsync(null!));
	}

	[Test]
	public void RunRawAsync_NullArguments_Throws()
	{
		var gh = new GitHubCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await gh.RunRawAsync(null!));
	}

	[Test]
	public void PrCreateAsync_NullTitle_Throws()
	{
		var gh = new GitHubCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await gh.PrCreateAsync(null!, "b"));
	}

	[Test]
	public void PrCreateAsync_NullBody_Throws()
	{
		var gh = new GitHubCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await gh.PrCreateAsync("t", null!));
	}

	[Test]
	public void ApiAsync_EmptyEndpoint_Throws()
	{
		var gh = new GitHubCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentException>(async () => await gh.ApiAsync(string.Empty));
	}

	[Test]
	public async Task Client_IsUsableThroughIGitHubCliInterface()
	{
		// Consumers depend on IGitHubCli (not the concrete class) and can substitute a mock in tests.
		var fake = new FakeExecutor(Ok("gh version 2.62.0\n"), Ok(string.Empty));
		IGitHubCli gh = new GitHubCli(fake);

		var version = await gh.VersionAsync();
		var authenticated = await gh.IsAuthenticatedAsync();
		Assert.Multiple(() =>
		{
			Assert.That(gh.Executable, Is.EqualTo("gh"));
			Assert.That(version, Is.EqualTo("gh version 2.62.0"));
			Assert.That(authenticated, Is.True);
		});
	}

	[Test]
	public void Exception_PublicConstructor_SetsFields_ForMocking()
	{
		var ex = new GitHubCliException("boom", exitCode: 4, stdErr: "gh error", arguments: "pr list", timedOut: true);

		Assert.Multiple(() =>
		{
			Assert.That(ex.Message, Is.EqualTo("boom"));
			Assert.That(ex.ExitCode, Is.EqualTo(4));
			Assert.That(ex.StdErr, Is.EqualTo("gh error"));
			Assert.That(ex.Arguments, Is.EqualTo("pr list"));
			Assert.That(ex.TimedOut, Is.True);
		});
	}

	[Test]
	public async Task RunAsync_StdinOverload_PassesStandardInput()
	{
		var fake = new FakeExecutor(Ok("{}"));
		await new GitHubCli(fake).RunAsync(["pr", "create", "--body-file", "-"], "PR body text");

		Assert.That(fake.StandardInputs[0], Is.EqualTo("PR body text"));
	}

	[Test]
	public void Environment_IsSnapshotIndependentOfCaller()
	{
		var env = new Dictionary<string, string> { ["GH_TOKEN"] = "x" };
		var gh = new GitHubCli(environment: env);
		env["GH_TOKEN"] = "MUTATED";

		Assert.Multiple(() =>
		{
			Assert.That(gh.Environment, Has.Count.EqualTo(1));
			Assert.That(gh.Environment!["GH_TOKEN"], Is.EqualTo("x"));
			Assert.That(new GitHubCli().Environment, Is.Null);
		});
	}

	[Test]
	public void GitHubOutputParser_ParseRepository_HandlesNullFields()
	{
		const string json = """{ "name": "r", "owner": { "login": "o" }, "description": null, "url": "u", "isPrivate": true, "defaultBranchRef": null }""";
		var repo = GitHubOutputParser.ParseRepository(json);

		Assert.Multiple(() =>
		{
			Assert.That(repo.Description, Is.Null);
			Assert.That(repo.IsPrivate, Is.True);
			Assert.That(repo.DefaultBranch, Is.Empty);
		});
	}

	// Exercises the real `gh` binary; excluded from the default CI run.
	[Test]
	[Explicit("requires the gh binary to be installed")]
	public async Task Integration_VersionAsync_MentionsGh()
	{
		var version = await new GitHubCli().VersionAsync();
		Assert.That(version.ToLowerInvariant(), Does.Contain("gh"));
	}
}
