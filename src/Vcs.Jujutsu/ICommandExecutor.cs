namespace Vcs.Jujutsu;

/// <summary>
/// Internal seam over process execution so <see cref="JujutsuCli"/> can be unit-tested without the
/// real <c>jj</c> binary. The production implementation is <see cref="ProcessKitCommandExecutor"/>.
/// </summary>
internal interface ICommandExecutor
{
	Task<JjCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
