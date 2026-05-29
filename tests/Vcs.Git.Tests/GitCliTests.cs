namespace Vcs.Git.Tests;

[TestFixture]
public class GitCliTests
{
	[Test]
	public void ExecutableName_IsGit()
	{
		Assert.That(GitCli.ExecutableName, Is.EqualTo("git"));
	}
}
