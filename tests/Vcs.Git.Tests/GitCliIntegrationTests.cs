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
		Assert.That(await git.CurrentBranchAsync(), Is.Not.Empty);
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
