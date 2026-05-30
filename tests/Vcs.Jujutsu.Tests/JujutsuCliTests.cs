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

		public Task<JjCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
		{
			Calls.Add(arguments);
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

	// Exercises the real `jj` binary; excluded from the default CI run.
	[Test]
	[Explicit("requires the jj binary to be installed")]
	public async Task Integration_VersionAsync_MentionsJj()
	{
		var version = await new JujutsuCli().VersionAsync();
		Assert.That(version.ToLowerInvariant(), Does.Contain("jj"));
	}
}
