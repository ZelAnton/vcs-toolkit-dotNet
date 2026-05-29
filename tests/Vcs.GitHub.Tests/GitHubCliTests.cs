namespace Vcs.GitHub.Tests;

[TestFixture]
public class GitHubCliTests
{
	[Test]
	public void ExecutableName_IsGh()
	{
		Assert.That(GitHubCli.ExecutableName, Is.EqualTo("gh"));
	}
}
