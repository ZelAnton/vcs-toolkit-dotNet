namespace Vcs.Jujutsu.Tests;

[TestFixture]
public class JujutsuCliTests
{
	private static readonly char Fs = (char)0x1f;
	private static readonly char Rs = (char)0x1e;

	private sealed class FakeExecutor(params JjCommandResult[] results) : ICommandExecutor
	{
		private readonly Queue<JjCommandResult> _results = new(results);

		public List<IReadOnlyList<string>> Calls { get; } = [];

		public List<TimeSpan?> Timeouts { get; } = [];

		public List<string?> StandardInputs { get; } = [];

		public Task<JjCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
		{
			Calls.Add(arguments);
			Timeouts.Add(timeout);
			StandardInputs.Add(standardInput);
			var result = _results.Count > 0 ? _results.Dequeue() : new JjCommandResult(string.Empty, string.Empty, 0);
			return Task.FromResult(result);
		}
	}

	private static JjCommandResult Ok(string stdOut) => new(stdOut, string.Empty, 0);

	[Test]
	public void DefaultExecutable_IsJj()
	{
		Assert.That(new JujutsuCli().Executable, Is.EqualTo("jj"));
	}

	[Test]
	public async Task VersionAsync_RunsVersionFlag_AndTrimsOutput()
	{
		var fake = new FakeExecutor(Ok("jj 0.38.0\n"));
		var jj = new JujutsuCli(fake);

		var version = await jj.VersionAsync();

		Assert.That(version, Is.EqualTo("jj 0.38.0"));
		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "--version" }));
	}

	[Test]
	public void RunAsync_NonZeroExit_Throws()
	{
		var fake = new FakeExecutor(new JjCommandResult(string.Empty, "Error: No such revision", 1));
		var jj = new JujutsuCli(fake);

		var ex = Assert.ThrowsAsync<JujutsuCliException>(async () => await jj.RunAsync(["log"]));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.ExitCode, Is.EqualTo(1));
			Assert.That(ex.StdErr, Is.EqualTo("Error: No such revision"));
			Assert.That(ex.Arguments, Is.EqualTo("log"));
		});
	}

	[Test]
	public async Task RunRawAsync_NonZeroExit_DoesNotThrow()
	{
		var fake = new FakeExecutor(new JjCommandResult("out", "err", 2));
		var jj = new JujutsuCli(fake);

		var result = await jj.RunRawAsync(["whatever"]);

		Assert.Multiple(() =>
		{
			Assert.That(result.IsSuccess, Is.False);
			Assert.That(result.ExitCode, Is.EqualTo(2));
			Assert.That(result.StdOut, Is.EqualTo("out"));
		});
	}

	[Test]
	public async Task DescribeAsync_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake);

		await jj.DescribeAsync("a message", revision: "@-");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "describe", "-r", "@-", "-m", "a message" }));
	}

	[Test]
	public async Task NewAsync_WithParentsAndMessage_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake);

		await jj.NewAsync("desc", parents: ["@-", "xyz"]);

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "new", "@-", "xyz", "-m", "desc" }));
	}

	[Test]
	public async Task NewAsync_Default_BuildsBareNew()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake);

		await jj.NewAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "new" }));
	}

	[Test]
	public async Task LogAsync_ParsesChanges_AndPassesLimit()
	{
		var output =
			$"chg1{Fs}commit1{Fs}false{Fs}First change{Rs}" +
			$"chg2{Fs}commit2{Fs}true{Fs}{Rs}";
		var fake = new FakeExecutor(Ok(output));
		var jj = new JujutsuCli(fake);

		var changes = await jj.LogAsync(limit: 5, revset: "::@");

		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0][0], Is.EqualTo("log"));
			Assert.That(fake.Calls[0], Does.Contain("--no-graph"));
			Assert.That(fake.Calls[0], Does.Contain("-r"));
			Assert.That(fake.Calls[0], Does.Contain("::@"));
			Assert.That(fake.Calls[0], Does.Contain("-n"));
			Assert.That(fake.Calls[0], Does.Contain("5"));
		});
		Assert.That(changes, Has.Count.EqualTo(2));
		Assert.Multiple(() =>
		{
			Assert.That(changes[0], Is.EqualTo(new JjChange("chg1", "commit1", "First change", false)));
			Assert.That(changes[1], Is.EqualTo(new JjChange("chg2", "commit2", string.Empty, true)));
		});
	}

	[Test]
	public async Task BookmarkSetAsync_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake);

		await jj.BookmarkSetAsync("main", revision: "@-");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "bookmark", "set", "main", "-r", "@-" }));
	}

	[Test]
	public async Task GitPushAsync_WithBookmark_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake);

		await jj.GitPushAsync("main");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "git", "push", "--bookmark", "main" }));
	}

	[Test]
	public async Task GitFetchAsync_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake);

		await jj.GitFetchAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "git", "fetch" }));
	}

	[Test]
	public async Task DefaultTimeout_IsAppliedToCommands()
	{
		var fake = new FakeExecutor(Ok("jj 0.38.0"));
		var jj = new JujutsuCli(fake, defaultTimeout: TimeSpan.FromSeconds(9));

		await jj.VersionAsync();

		Assert.That(jj.DefaultTimeout, Is.EqualTo(TimeSpan.FromSeconds(9)));
		Assert.That(fake.Timeouts[0], Is.EqualTo(TimeSpan.FromSeconds(9)));
	}

	[Test]
	public async Task PerCallTimeout_OverridesDefault()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		var jj = new JujutsuCli(fake, defaultTimeout: TimeSpan.FromSeconds(9));

		await jj.RunAsync(["status"], TimeSpan.FromSeconds(3));

		Assert.That(fake.Timeouts[0], Is.EqualTo(TimeSpan.FromSeconds(3)));
	}

	[Test]
	public void RunAsync_Timeout_ThrowsWithTimedOutFlag()
	{
		var fake = new FakeExecutor(new JjCommandResult(string.Empty, string.Empty, -1) { WasTimedOut = true });
		var jj = new JujutsuCli(fake);

		var ex = Assert.ThrowsAsync<JujutsuCliException>(async () => await jj.RunAsync(["git", "fetch"], TimeSpan.FromSeconds(1)));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.TimedOut, Is.True);
			Assert.That(ex.Message, Does.Contain("timed out"));
		});
	}

	[Test]
	public void Constructor_NonPositiveTimeout_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new JujutsuCli(defaultTimeout: TimeSpan.FromSeconds(-1)));
	}

	[Test]
	public void RunAsync_ExecutableNotFound_ThrowsCliException()
	{
		var jj = new JujutsuCli(executable: "vcs-toolkit-no-such-binary-zzz");

		var ex = Assert.ThrowsAsync<JujutsuCliException>(async () => await jj.RunAsync(["status"]));
		Assert.Multiple(() =>
		{
			Assert.That(ex!.Message, Does.Contain("Could not start"));
			Assert.That(ex.InnerException, Is.InstanceOf<System.ComponentModel.Win32Exception>());
		});
	}

	[Test]
	public async Task StatusAsync_BuildsArguments_AndTrims()
	{
		var fake = new FakeExecutor(Ok("Working copy changes:\n"));
		var status = await new JujutsuCli(fake).StatusAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "status" }));
		Assert.That(status, Is.EqualTo("Working copy changes:"));
	}

	[Test]
	public async Task BookmarkListAsync_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).BookmarkListAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "bookmark", "list" }));
	}

	[Test]
	public async Task GitPushAsync_NoBookmark_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).GitPushAsync();

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "git", "push" }));
	}

	[Test]
	public async Task DescribeAsync_DefaultRevision_TargetsWorkingCopy()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).DescribeAsync("msg");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "describe", "-r", "@", "-m", "msg" }));
	}

	[Test]
	public async Task BookmarkSetAsync_DefaultRevision_TargetsWorkingCopy()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).BookmarkSetAsync("main");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "bookmark", "set", "main", "-r", "@" }));
	}

	[Test]
	public async Task NewAsync_MessageOnly_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).NewAsync("desc");

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "new", "-m", "desc" }));
	}

	[Test]
	public async Task NewAsync_ParentsOnly_BuildsArguments()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).NewAsync(parents: ["@-"]);

		Assert.That(fake.Calls[0], Is.EqualTo(new[] { "new", "@-" }));
	}

	[Test]
	public async Task LogAsync_NoArguments_OmitsRevsetAndLimit()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).LogAsync();

		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0][0], Is.EqualTo("log"));
			Assert.That(fake.Calls[0], Does.Contain("--no-graph"));
			Assert.That(fake.Calls[0], Does.Contain("-T"));
			Assert.That(fake.Calls[0], Does.Not.Contain("-r"));
			Assert.That(fake.Calls[0], Does.Not.Contain("-n"));
		});
	}

	[Test]
	public async Task LogAsync_EmptyOutput_ReturnsEmpty()
	{
		var changes = await new JujutsuCli(new FakeExecutor(Ok(string.Empty))).LogAsync();

		Assert.That(changes, Is.Empty);
	}

	[Test]
	public async Task LogAsync_RevsetOnly_OmitsLimit()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).LogAsync(revset: "@");

		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0], Does.Contain("-r"));
			Assert.That(fake.Calls[0], Does.Contain("@"));
			Assert.That(fake.Calls[0], Does.Not.Contain("-n"));
		});
	}

	[Test]
	public async Task LogAsync_LimitOnly_OmitsRevset()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).LogAsync(limit: 3);

		Assert.Multiple(() =>
		{
			Assert.That(fake.Calls[0], Does.Contain("-n"));
			Assert.That(fake.Calls[0], Does.Contain("3"));
			Assert.That(fake.Calls[0], Does.Not.Contain("-r"));
		});
	}

	[Test]
	public void WorkingDirectory_RoundTrips()
	{
		Assert.Multiple(() =>
		{
			Assert.That(new JujutsuCli(workingDirectory: "/repo").WorkingDirectory, Is.EqualTo("/repo"));
			Assert.That(new JujutsuCli().WorkingDirectory, Is.Null);
		});
	}

	[Test]
	public void Constructor_EmptyExecutable_Throws()
	{
		Assert.Throws<ArgumentException>(() => new JujutsuCli(executable: string.Empty));
	}

	[Test]
	public void RunAsync_NullArguments_Throws()
	{
		var jj = new JujutsuCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await jj.RunAsync(null!));
	}

	[Test]
	public void RunRawAsync_NullArguments_Throws()
	{
		var jj = new JujutsuCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await jj.RunRawAsync(null!));
	}

	[Test]
	public void DescribeAsync_NullMessage_Throws()
	{
		var jj = new JujutsuCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentNullException>(async () => await jj.DescribeAsync(null!));
	}

	[Test]
	public void DescribeAsync_EmptyRevision_Throws()
	{
		var jj = new JujutsuCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentException>(async () => await jj.DescribeAsync("msg", revision: string.Empty));
	}

	[Test]
	public void BookmarkSetAsync_EmptyName_Throws()
	{
		var jj = new JujutsuCli(new FakeExecutor(Ok(string.Empty)));
		Assert.ThrowsAsync<ArgumentException>(async () => await jj.BookmarkSetAsync(string.Empty));
	}

	[Test]
	public async Task Client_IsUsableThroughIJujutsuCliInterface()
	{
		// Consumers depend on IJujutsuCli (not the concrete class) and can substitute a mock in tests.
		var fake = new FakeExecutor(Ok("jj 0.38.0\n"));
		IJujutsuCli jj = new JujutsuCli(fake);

		var version = await jj.VersionAsync();
		Assert.Multiple(() =>
		{
			Assert.That(jj.Executable, Is.EqualTo("jj"));
			Assert.That(version, Is.EqualTo("jj 0.38.0"));
		});
	}

	[Test]
	public void Exception_PublicConstructor_SetsFields_ForMocking()
	{
		var ex = new JujutsuCliException("boom", exitCode: 1, stdErr: "Error", arguments: "log", timedOut: true);

		Assert.Multiple(() =>
		{
			Assert.That(ex.Message, Is.EqualTo("boom"));
			Assert.That(ex.ExitCode, Is.EqualTo(1));
			Assert.That(ex.StdErr, Is.EqualTo("Error"));
			Assert.That(ex.Arguments, Is.EqualTo("log"));
			Assert.That(ex.TimedOut, Is.True);
		});
	}

	[Test]
	public async Task RunAsync_StdinOverload_PassesStandardInput()
	{
		var fake = new FakeExecutor(Ok(string.Empty));
		await new JujutsuCli(fake).RunAsync(["describe", "--stdin"], "stdin description");

		Assert.That(fake.StandardInputs[0], Is.EqualTo("stdin description"));
	}

	[Test]
	public void Environment_IsSnapshotIndependentOfCaller()
	{
		var env = new Dictionary<string, string> { ["JJ_USER"] = "CI" };
		var jj = new JujutsuCli(environment: env);
		env["JJ_USER"] = "MUTATED";

		Assert.Multiple(() =>
		{
			Assert.That(jj.Environment, Has.Count.EqualTo(1));
			Assert.That(jj.Environment!["JJ_USER"], Is.EqualTo("CI"));
			Assert.That(new JujutsuCli().Environment, Is.Null);
		});
	}

	[Test]
	public void JjOutputParser_ParseChanges_ParsesAndToleratesEmptyDescription()
	{
		var output =
			$"chg1{Fs}commit1{Fs}false{Fs}First change{Rs}" +
			$"chg2{Fs}commit2{Fs}true{Fs}{Rs}";
		var changes = JjOutputParser.ParseChanges(output);

		Assert.That(changes, Is.EqualTo(new[]
		{
			new JjChange("chg1", "commit1", "First change", false),
			new JjChange("chg2", "commit2", string.Empty, true),
		}));
	}

	// Exercises the real `jj` binary; excluded from the default CI run.
	[Test]
	[Explicit("requires the jj binary to be installed")]
	public async Task Integration_VersionAsync_MentionsJj()
	{
		var version = await new JujutsuCli().VersionAsync();
		Assert.That(version.ToLowerInvariant(), Does.Contain("jj"));
	}
}
