namespace Vcs.Git.Tests;

// Drives the real `git` binary in a throwaway repository to validate argument construction and
// output parsing against actual git output. Excluded from the default `dotnet test` run.
[TestFixture]
[Explicit("requires the git binary to be installed")]
public class GitCliIntegrationTests
{
	private string _repo = string.Empty;

	[SetUp]
	public void CreateRepo()
	{
		_repo = Path.Combine(Path.GetTempPath(), "vcs-git-it-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_repo);
	}

	[TearDown]
	public void DeleteRepo()
	{
		ForceDelete(_repo);
	}

	[Test]
	public async Task InitCommitLogStatus_RoundTrip()
	{
		var git = new GitCli(workingDirectory: _repo);

		await git.InitAsync();
		await git.RunAsync(["config", "user.email", "ci@example.com"]);
		await git.RunAsync(["config", "user.name", "CI"]);
		await git.RunAsync(["config", "commit.gpgsign", "false"]);

		await File.WriteAllTextAsync(Path.Combine(_repo, "file.txt"), "hello\n");
		await git.StageAsync(["file.txt"]);
		var hash = await git.CommitAsync("Initial commit");

		Assert.Multiple(() =>
		{
			Assert.That(hash, Has.Length.EqualTo(40));
			Assert.That(hash.All(Uri.IsHexDigit), Is.True, "commit hash should be hex");
		});

		var commits = await git.LogAsync();
		Assert.That(commits, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(commits[0].Hash, Is.EqualTo(hash));
			Assert.That(commits[0].Subject, Is.EqualTo("Initial commit"));
			Assert.That(commits[0].Author, Is.EqualTo("CI"));
		});

		Assert.That(await git.StatusAsync(), Is.Empty, "working tree should be clean after commit");
		var currentBranch = await git.CurrentBranchAsync();
		Assert.That(currentBranch, Is.Not.Empty);

		var branches = await git.BranchesAsync();
		Assert.That(branches, Has.Exactly(1).Matches<GitBranch>(b => b.Name == currentBranch && b.IsCurrent));
		Assert.That(await git.RevParseAsync("HEAD"), Is.EqualTo(hash));
	}

	[Test]
	public async Task StdinAndEnvironment_RoundTrip()
	{
		// stdin: `git hash-object --stdin` reads stdin and prints the object's SHA-1 — fully deterministic.
		var git = new GitCli(workingDirectory: _repo);
		await git.InitAsync();
		// Disable commit signing so the commit below succeeds on a host whose global config sets commit.gpgsign=true.
		await git.RunAsync(["config", "commit.gpgsign", "false"]);
		var hash = await git.RunAsync(["hash-object", "--stdin"], "hello\n");
		Assert.That(hash, Is.EqualTo("ce013625030ba8dba906f756967f9e9ca394464a"), "SHA-1 of \"hello\\n\"");

		// environment: GIT_AUTHOR_* is honoured by the committed author.
		var env = new Dictionary<string, string>
		{
			["GIT_AUTHOR_NAME"] = "Env Author",
			["GIT_AUTHOR_EMAIL"] = "env@example.com",
			["GIT_COMMITTER_NAME"] = "Env Author",
			["GIT_COMMITTER_EMAIL"] = "env@example.com",
		};
		var gitWithEnv = new GitCli(workingDirectory: _repo, environment: env);
		await File.WriteAllTextAsync(Path.Combine(_repo, "f.txt"), "x");
		await gitWithEnv.StageAsync(["f.txt"]);
		await gitWithEnv.CommitAsync("env commit");

		var commits = await gitWithEnv.LogAsync(maxCount: 1);
		Assert.That(commits[0].Author, Is.EqualTo("Env Author"));
	}

	[Test]
	public async Task StatusAsync_NonAsciiPath_ReturnedVerbatim()
	{
		var git = new GitCli(workingDirectory: _repo);
		await git.InitAsync();

		const string name = "café-naïve-日本語.txt";
		await File.WriteAllTextAsync(Path.Combine(_repo, name), "x");

		var entries = await git.StatusAsync();
		Assert.That(entries.Any(e => e.Path == name), Is.True,
			"non-ASCII path must come back verbatim, not C-quoted/octal-escaped");
	}

	[Test]
	public async Task StatusAsync_ReportsUntrackedAndStaged()
	{
		var git = new GitCli(workingDirectory: _repo);
		await git.InitAsync();

		await File.WriteAllTextAsync(Path.Combine(_repo, "untracked.txt"), "x");
		await File.WriteAllTextAsync(Path.Combine(_repo, "staged.txt"), "y");
		await git.StageAsync(["staged.txt"]);

		var entries = await git.StatusAsync();
		Assert.Multiple(() =>
		{
			Assert.That(entries.Any(e => e.Path == "untracked.txt" && e is { Index: '?', WorkTree: '?' }), Is.True);
			Assert.That(entries.Any(e => e.Path == "staged.txt" && e.Index == 'A'), Is.True);
		});
	}

	private static void ForceDelete(string path)
	{
		if (!Directory.Exists(path))
			return;

		foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
		{
			try
			{
				File.SetAttributes(file, FileAttributes.Normal);
			}
			catch (SystemException)
			{
				// best-effort: git packs objects read-only; if clearing the attribute fails the
				// delete below still tries, and a leftover temp dir is harmless for a test.
			}
		}

		try
		{
			Directory.Delete(path, recursive: true);
		}
		catch (IOException)
		{
			// best-effort cleanup of a temp directory; a leftover under %TEMP% is not worth failing a test over.
		}
		catch (UnauthorizedAccessException)
		{
			// same rationale: a locked/read-only leftover under %TEMP% must not fail the test.
		}
	}
}
