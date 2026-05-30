namespace Vcs.Git.Tests;

[TestFixture]
public class GitCliTests
{
	private const char Fs = '';
	private const char Rs = '';

	private sealed class FakeExecutor(params GitCommandResult[] results) : ICommandExecutor
	{
		private readonly Queue<GitCommandResult> _results = new(results);

		public List<IReadOnlyList<string>> Calls { get; } = [];

		public List<TimeSpan?> Timeouts { get; } = [];

		public Task<GitCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, CancellationToken cancellationToken)
		{
			Calls.Add(arguments);
			Timeouts.Add(timeout);
			var result = _results.Count > 0 ? _results.Dequeue() : new GitCommandResult(string.Empty, string.Empty, 0);
			return Task.FromResult(result);
		}
	}

	private static GitCommandResult Ok(string stdOut) => new(stdOut, string.Empty, 0);

	[Test]
	public void DefaultExecutable_IsGit()
	{
		Assert.That(new GitCli().Executable, Is.EqualTo("git"));
	}

	[Test]
	public async Task VersionAsync_RunsVersionFlag_AndTrimsOutput()
	{
		var fake = new FakeExecutor(Ok("git version 2.45.0\n"));
		var git = new GitCli(fake);

		var version = await git.VersionAsync();

		Assert.That(version, Is.EqualTo("git version 2.45.0"));
		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "--version" }));
	}

	[Test]
	public void RunAsync_NonZeroExit_Throws()
	{
		var fake = new FakeExecutor(new GitCommandResult(string.Empty, "fatal: not a repo", 128));
		var git = new GitCli(fake);

		var ex = Assert.ThrowsAsync<GitCliException>(async () => await git.RunAsync(["status"]));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.ExitCode, Is.EqualTo(128));
			Assert.That(ex.StdErr, Is.EqualTo("fatal: not a repo"));
			Assert.That(ex.Arguments, Is.EqualTo("status"));
		});
	}

	[Test]
	public async Task RunRawAsync_NonZeroExit_DoesNotThrow()
	{
		var fake = new FakeExecutor(new GitCommandResult("out", "err", 1));
		var git = new GitCli(fake);

		var result = await git.RunRawAsync(["whatever"]);

		Assert.Multiple(() =>
		{
			Assert.That(result.IsSuccess, Is.False);
			Assert.That(result.ExitCode, Is.EqualTo(1));
			Assert.That(result.StdOut, Is.EqualTo("out"));
			Assert.That(result.StdErr, Is.EqualTo("err"));
		});
	}

	[Test]
	public async Task StatusAsync_ParsesPorcelainEntries()
	{
		var fake = new FakeExecutor(Ok("M  staged.cs\n M unstaged.cs\n?? new.cs\nR  old.cs -> renamed.cs"));
		var git = new GitCli(fake);

		var entries = await git.StatusAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "status", "--porcelain=v1" }));
		Assert.That(entries, Has.Count.EqualTo(4));
		Assert.Multiple(() =>
		{
			Assert.That(entries[0], Is.EqualTo(new GitStatusEntry('M', ' ', "staged.cs")));
			Assert.That(entries[1], Is.EqualTo(new GitStatusEntry(' ', 'M', "unstaged.cs")));
			Assert.That(entries[2], Is.EqualTo(new GitStatusEntry('?', '?', "new.cs")));
			Assert.That(entries[3], Is.EqualTo(new GitStatusEntry('R', ' ', "renamed.cs")));
		});
	}

	[Test]
	public async Task StatusAsync_CleanTree_ReturnsEmpty()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));

		Assert.That(await git.StatusAsync(), Is.Empty);
	}

	[Test]
	public async Task StageAsync_BuildsAddArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var git = new GitCli(fake);

		await git.StageAsync(["a.cs", "b.cs"]);

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "add", "--", "a.cs", "b.cs" }));
	}

	[Test]
	public async Task CommitAsync_CommitsThenReturnsResolvedHash()
	{
		var fake = new FakeExecutor(Ok("[main abc] msg"), Ok("abc123def456\n"));
		var git = new GitCli(fake);

		var hash = await git.CommitAsync("a message", all: true);

		Assert.That(hash, Is.EqualTo("abc123def456"));
		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0], Is.EqualTo(new[] { "commit", "-m", "a message", "-a" }));
			Assert.That(fake.Calls[1], Is.EqualTo(new[] { "rev-parse", "HEAD" }));
		});
	}

	[Test]
	public async Task LogAsync_ParsesRecordsAndAppliesMaxCount()
	{
		var output =
			$"h1{Fs}s1{Fs}Alice{Fs}2026-05-30T10:00:00+00:00{Fs}First subject{Rs}\n" +
			$"h2{Fs}s2{Fs}Bob{Fs}2026-05-29T09:30:00+00:00{Fs}Second subject{Rs}";
		var fake = new FakeExecutor(Ok(output));
		var git = new GitCli(fake);

		var commits = await git.LogAsync(maxCount: 2);

		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0][0], Is.EqualTo("log"));
			Assert.That(fake.Calls[0], Does.Contain("-n"));
			Assert.That(fake.Calls[0], Does.Contain("2"));
		});
		Assert.That(commits, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(commits[0].Hash, Is.EqualTo("h1"));
			Assert.That(commits[0].ShortHash, Is.EqualTo("s1"));
			Assert.That(commits[0].Author, Is.EqualTo("Alice"));
			Assert.That(commits[0].Date, Is.EqualTo(new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero)));
			Assert.That(commits[0].Subject, Is.EqualTo("First subject"));
			Assert.That(commits[1].Author, Is.EqualTo("Bob"));
		});
	}

	[Test]
	public async Task CurrentBranchAsync_ReturnsTrimmedBranch()
	{
		var fake = new FakeExecutor(Ok("main\n"));
		var git = new GitCli(fake);

		Assert.That(await git.CurrentBranchAsync(), Is.EqualTo("main"));
		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "branch", "--show-current" }));
	}

	[Test]
	public async Task DefaultTimeout_IsAppliedToCommands()
	{
		var fake = new FakeExecutor(Ok("git version 2.45.0"));
		var git = new GitCli(fake, defaultTimeout: TimeSpan.FromSeconds(7));

		await git.VersionAsync();

		Assert.That(git.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
		Assert.That(fake.Timeouts[0], Is.EqualTo(TimeSpan.FromSeconds(7)));
	}

	[Test]
	public async Task PerCallTimeout_OverridesDefault()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var git = new GitCli(fake, defaultTimeout: TimeSpan.FromSeconds(7));

		await git.RunAsync(["status"], TimeSpan.FromSeconds(2));

		Assert.That(fake.Timeouts[0], Is.EqualTo(TimeSpan.FromSeconds(2)));
	}

	[Test]
	public void RunAsync_Timeout_ThrowsWithTimedOutFlag()
	{
		var fake = new FakeExecutor(new GitCommandResult(string.Empty, string.Empty, -1) { WasTimedOut = true });
		var git = new GitCli(fake);

		var ex = Assert.ThrowsAsync<GitCliException>(async () => await git.RunAsync(["fetch"], TimeSpan.FromSeconds(1)));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.TimedOut, Is.True);
			Assert.That(ex.Message, Does.Contain("timed out"));
		});
	}

	[Test]
	public async Task RunRawAsync_Timeout_DoesNotThrow_AndReportsFlag()
	{
		var fake = new FakeExecutor(new GitCommandResult(string.Empty, string.Empty, -1) { WasTimedOut = true });
		var git = new GitCli(fake);

		var result = await git.RunRawAsync(["fetch"], TimeSpan.FromSeconds(1));

		Assert.That(result.WasTimedOut, Is.True);
	}

	[Test]
	public void Constructor_NonPositiveTimeout_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new GitCli(defaultTimeout: TimeSpan.Zero));
	}

	[Test]
	public void RunAsync_ExecutableNotFound_ThrowsCliException()
	{
		var git = new GitCli(executable: "vcs-toolkit-no-such-binary-zzz");

		var ex = Assert.ThrowsAsync<GitCliException>(async () => await git.RunAsync(["status"]));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.Message, Does.Contain("Could not start"));
			Assert.That(ex.InnerException, Is.InstanceOf<System.ComponentModel.Win32Exception>());
		});
	}

	[Test]
	public void RunRawAsync_ExecutableNotFound_ThrowsCliException()
	{
		var git = new GitCli(executable: "vcs-toolkit-no-such-binary-zzz");

		Assert.ThrowsAsync<GitCliException>(async () => await git.RunRawAsync(["status"]));
	}

	[Test]
	public async Task InitAsync_Default_BuildsInit()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new GitCli(fake).InitAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "init" }));
	}

	[Test]
	public async Task InitAsync_Bare_AddsBareFlag()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new GitCli(fake).InitAsync(bare: true);

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "init", "--bare" }));
	}

	[Test]
	public async Task CreateBranchAsync_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new GitCli(fake).CreateBranchAsync("feature");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "branch", "feature" }));
	}

	[Test]
	public async Task CheckoutAsync_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new GitCli(fake).CheckoutAsync("feature");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "checkout", "feature" }));
	}

	[Test]
	public async Task CommitAsync_WithoutAll_OmitsAllFlag()
	{
		var fake = new FakeExecutor(Ok("[main abc] msg"), Ok("abc\n"));
		await new GitCli(fake).CommitAsync("msg");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "commit", "-m", "msg" }));
	}

	[Test]
	public async Task LogAsync_NoMaxCount_OmitsLimit()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new GitCli(fake).LogAsync();

		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0][0], Is.EqualTo("log"));
			Assert.That(fake.Calls[0], Does.Not.Contain("-n"));
		});
	}

	[Test]
	public async Task LogAsync_EmptyOutput_ReturnsEmpty()
	{
		var commits = await new GitCli(new FakeExecutor(Ok(string.Empty))).LogAsync();

		Assert.That(commits, Is.Empty);
	}

	[Test]
	public async Task StatusAsync_HandlesCrlf_AndStripsCarriageReturn()
	{
		var fake = new FakeExecutor(Ok("M  a.cs\r\n M b.cs\r\n"));
		var entries = await new GitCli(fake).StatusAsync();

		Assert.That(entries, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(entries[0], Is.EqualTo(new GitStatusEntry('M', ' ', "a.cs")));
			Assert.That(entries[1], Is.EqualTo(new GitStatusEntry(' ', 'M', "b.cs")));
		});
	}

	[Test]
	public async Task StatusAsync_SkipsTooShortLines()
	{
		var fake = new FakeExecutor(Ok("M  real.cs\nXX\n"));
		var entries = await new GitCli(fake).StatusAsync();

		Assert.That(entries, Has.Count.EqualTo(1));
		Assert.That(entries[0].Path, Is.EqualTo("real.cs"));
	}

	[Test]
	public async Task RunAsync_Success_ReturnsTrimmedStdout()
	{
		var result = await new GitCli(new FakeExecutor(Ok("  hello \n"))).RunAsync(["whatever"]);

		Assert.That(result, Is.EqualTo("hello"));
	}

	[Test]
	public void WorkingDirectory_RoundTrips()
	{
		Assert.Multiple(() =>
		{
			Assert.That(new GitCli(workingDirectory: "/repo").WorkingDirectory, Is.EqualTo("/repo"));
			Assert.That(new GitCli().WorkingDirectory, Is.Null);
		});
	}

	[Test]
	public void Constructor_EmptyExecutable_Throws()
	{
		Assert.Throws<ArgumentException>(() => new GitCli(executable: string.Empty));
	}

	[Test]
	public void RunAsync_NullArguments_Throws()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await git.RunAsync(null!));
	}

	[Test]
	public void RunRawAsync_NullArguments_Throws()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await git.RunRawAsync(null!));
	}

	[Test]
	public void StageAsync_NullPaths_Throws()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await git.StageAsync(null!));
	}

	[Test]
	public void CommitAsync_NullMessage_Throws()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await git.CommitAsync(null!));
	}

	[Test]
	public void CreateBranchAsync_EmptyName_Throws()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentException>(async () => await git.CreateBranchAsync(string.Empty));
	}

	[Test]
	public void CheckoutAsync_EmptyReference_Throws()
	{
		var git = new GitCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentException>(async () => await git.CheckoutAsync(string.Empty));
	}

	[Test]
	public async Task Client_IsUsableThroughIGitCliInterface()
	{
		// Consumers depend on IGitCli (not the concrete class) and can substitute a mock in tests.
		var fake = new FakeExecutor(Ok("git version 2.45.0\n"), Ok("main\n"));
		IGitCli git = new GitCli(fake);

		var version = await git.VersionAsync();
		var branch = await git.CurrentBranchAsync();
		Assert.Multiple(() =>
		{
			Assert.That(git.Executable, Is.EqualTo("git"));
			Assert.That(version, Is.EqualTo("git version 2.45.0"));
			Assert.That(branch, Is.EqualTo("main"));
		});
	}

	[Test]
	public void Exception_PublicConstructor_SetsFields_ForMocking()
	{
		var ex = new GitCliException("boom", exitCode: 128, stdErr: "fatal", arguments: "status", timedOut: true);

		Assert.Multiple(() =>
		{
			Assert.That(ex.Message, Is.EqualTo("boom"));
			Assert.That(ex.ExitCode, Is.EqualTo(128));
			Assert.That(ex.StdErr, Is.EqualTo("fatal"));
			Assert.That(ex.Arguments, Is.EqualTo("status"));
			Assert.That(ex.TimedOut, Is.True);
		});
	}

	// Exercises the real `git` binary; excluded from the default CI run.
	[Test]
	[Explicit("requires the git binary to be installed")]
	public async Task Integration_VersionAsync_MentionsGit()
	{
		var version = await new GitCli().VersionAsync();
		Assert.That(version.ToLowerInvariant(), Does.Contain("git"));
	}
}
