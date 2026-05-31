namespace Vcs.GitHub;

/// <summary>
/// Internal seam over process execution so <see cref="GitHubCli"/> can be unit-tested without the
/// real <c>gh</c> binary. The production implementation is <see cref="ProcessKitCommandExecutor"/>.
/// </summary>
internal interface ICommandExecutor
{
	Task<GitHubCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken);
}
