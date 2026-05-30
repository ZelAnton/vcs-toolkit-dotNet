namespace Vcs.GitHub;

/// <summary>
/// Abstraction over the <c>gh</c> (GitHub CLI) client implemented by <see cref="GitHubCli"/>.
/// Depend on this interface (rather than the concrete class) to keep call sites testable — it can
/// be substituted with a mock/fake in unit tests (e.g. <c>Substitute.For&lt;IGitHubCli&gt;()</c> with
/// NSubstitute, or <c>new Mock&lt;IGitHubCli&gt;()</c> with Moq). Every public operation of
/// <see cref="GitHubCli"/> is exposed here; construction is done via <see cref="GitHubCli"/>'s constructors.
/// </summary>
public interface IGitHubCli
{
	/// <summary>The underlying executable this client invokes.</summary>
	string Executable { get; }

	/// <summary>The process working directory, or <c>null</c> to inherit the host process's.</summary>
	string? WorkingDirectory { get; }

	/// <summary>The timeout applied to commands that do not specify their own, or <c>null</c> for none.</summary>
	TimeSpan? DefaultTimeout { get; }

	/// <summary>
	/// Runs <c>gh</c> with the given arguments (using <see cref="DefaultTimeout"/>) and returns the
	/// full result without throwing on a non-zero exit code.
	/// </summary>
	Task<GitHubCommandResult> RunRawAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments, killing it after <paramref name="timeout"/>, and
	/// returns the full result without throwing on a non-zero exit code (a timeout is reported via
	/// <see cref="GitHubCommandResult.WasTimedOut"/>).
	/// </summary>
	Task<GitHubCommandResult> RunRawAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments (using <see cref="DefaultTimeout"/>), throwing
	/// <see cref="GitHubCliException"/> on a non-zero exit code, and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments, killing it after <paramref name="timeout"/>, throwing
	/// <see cref="GitHubCliException"/> on a non-zero exit (including a timeout, where
	/// <see cref="GitHubCliException.TimedOut"/> is <c>true</c>), and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>Returns the installed GitHub CLI version string (<c>gh --version</c>).</summary>
	Task<string> VersionAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns <c>true</c> when there is an authenticated <c>gh</c> session (<c>gh auth status</c>
	/// exits zero), <c>false</c> otherwise.
	/// </summary>
	Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns metadata for a repository (<c>gh repo view --json</c>). Pass <paramref name="repository"/>
	/// as <c>OWNER/REPO</c> (or a URL); when <c>null</c>, the repository for the working directory is used.
	/// </summary>
	Task<GitHubRepository> RepoViewAsync(string? repository = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists pull requests (<c>gh pr list --json</c>), optionally filtered by <paramref name="state"/>
	/// (<c>open</c>, <c>closed</c>, <c>merged</c> or <c>all</c>) and capped to <paramref name="limit"/>.
	/// </summary>
	Task<IReadOnlyList<GitHubPullRequest>> PrListAsync(string? state = null, int? limit = null, CancellationToken cancellationToken = default);

	/// <summary>Returns a single pull request by number (<c>gh pr view &lt;number&gt; --json</c>).</summary>
	Task<GitHubPullRequest> PrViewAsync(int number, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a pull request (<c>gh pr create</c>) and returns the new pull request's URL.
	/// <paramref name="baseBranch"/> sets the target branch (defaults to the repository default).
	/// </summary>
	Task<string> PrCreateAsync(string title, string body, string? baseBranch = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Calls the GitHub REST/GraphQL API through <c>gh api</c> and returns the raw response body.
	/// <paramref name="method"/> overrides the HTTP method (e.g. <c>POST</c>).
	/// </summary>
	Task<string> ApiAsync(string endpoint, string? method = null, CancellationToken cancellationToken = default);
}
