namespace Vcs.Jujutsu.Tests;

[TestFixture]
public class JujutsuCliTests
{
	[Test]
	public void ExecutableName_IsJj()
	{
		Assert.That(JujutsuCli.ExecutableName, Is.EqualTo("jj"));
	}
}
