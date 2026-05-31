namespace Vcs.Git;

/// <summary>
/// Internal seam over process execution so <see cref="GitCli"/> can be unit-tested without the
/// real <c>git</c> binary. The production implementation is <see cref="ProcessKitCommandExecutor"/>.
/// </summary>
internal interface ICommandExecutor
{
	Task<GitCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken);
}
