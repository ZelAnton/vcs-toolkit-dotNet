namespace Vcs.Jujutsu.Tests;

// Drives the real `jj` binary in a throwaway repository to validate the log template and output
// parsing against actual jj output. Excluded from the default `dotnet test` run.
[TestFixture]
[Explicit("requires the jj binary to be installed")]
public class JujutsuCliIntegrationTests
{
	private string _repo = string.Empty;

	[SetUp]
	public void CreateRepo()
	{
		_repo = Path.Combine(Path.GetTempPath(), "vcs-jj-it-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_repo);
	}

	[TearDown]
	public void DeleteRepo()
	{
		ForceDelete(_repo);
	}

	[Test]
	public async Task DescribeNewLog_RoundTrip()
	{
		var jj = new JujutsuCli(workingDirectory: _repo);

		await jj.RunAsync(["git", "init"]);
		await jj.RunAsync(["config", "set", "--repo", "user.name", "CI"]);
		await jj.RunAsync(["config", "set", "--repo", "user.email", "ci@example.com"]);

		await jj.DescribeAsync("hello");

		var current = await jj.LogAsync(limit: 1, revset: "@");
		Assert.That(current, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(current[0].Description, Is.EqualTo("hello"));
			Assert.That(current[0].ChangeId, Is.Not.Empty);
			Assert.That(current[0].CommitId, Is.Not.Empty);
		});

		await jj.NewAsync("next");

		var stack = await jj.LogAsync(revset: "::@");
		var descriptions = stack.Select(c => c.Description).ToList();
		Assert.That(descriptions, Does.Contain("hello"));
		Assert.That(descriptions, Does.Contain("next"));
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
				// best-effort: jj/git store objects read-only; if clearing the attribute fails the
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
