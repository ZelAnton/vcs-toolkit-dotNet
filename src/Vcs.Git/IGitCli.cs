namespace Vcs.Git;

/// <summary>
/// Abstraction over the <c>git</c> command-line client implemented by <see cref="GitCli"/>.
/// Depend on this interface (rather than the concrete class) to keep call sites testable — it can
/// be substituted with a mock/fake in unit tests (e.g. <c>Substitute.For&lt;IGitCli&gt;()</c> with
/// NSubstitute, or <c>new Mock&lt;IGitCli&gt;()</c> with Moq). Every public operation of
/// <see cref="GitCli"/> is exposed here; construction is done via <see cref="GitCli"/>'s constructors.
/// </summary>
public interface IGitCli
{
	/// <summary>The underlying executable this client invokes.</summary>
	string Executable { get; }

	/// <summary>The process working directory, or <c>null</c> to inherit the host process's.</summary>
	string? WorkingDirectory { get; }

	/// <summary>The timeout applied to commands that do not specify their own, or <c>null</c> for none.</summary>
	TimeSpan? DefaultTimeout { get; }

	/// <summary>Environment variables applied (on top of the inherited environment) to every command, or <c>null</c>.</summary>
	IReadOnlyDictionary<string, string>? Environment { get; }

	/// <summary>
	/// Runs <c>git</c> with the given arguments (using <see cref="DefaultTimeout"/>) and returns the
	/// full result without throwing on a non-zero exit code.
	/// </summary>
	Task<GitCommandResult> RunRawAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>git</c> with the given arguments, killing it after <paramref name="timeout"/>, and
	/// returns the full result without throwing on a non-zero exit code (a timeout is reported via
	/// <see cref="GitCommandResult.WasTimedOut"/>).
	/// </summary>
	Task<GitCommandResult> RunRawAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>git</c> with the given arguments, piping <paramref name="standardInput"/> to stdin
	/// (optionally killing it after <paramref name="timeout"/>, else <see cref="DefaultTimeout"/>), and
	/// returns the full result without throwing on a non-zero exit code.
	/// </summary>
	Task<GitCommandResult> RunRawAsync(IEnumerable<string> arguments, string standardInput, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>git</c> with the given arguments (using <see cref="DefaultTimeout"/>), throwing
	/// <see cref="GitCliException"/> on a non-zero exit code, and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>git</c> with the given arguments, killing it after <paramref name="timeout"/>, throwing
	/// <see cref="GitCliException"/> on a non-zero exit (including a timeout, where
	/// <see cref="GitCliException.TimedOut"/> is <c>true</c>), and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>git</c> with the given arguments, piping <paramref name="standardInput"/> to stdin
	/// (optionally killing it after <paramref name="timeout"/>, else <see cref="DefaultTimeout"/>),
	/// throwing <see cref="GitCliException"/> on a non-zero exit, and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, string standardInput, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

	/// <summary>Returns the installed Git version string (<c>git --version</c>).</summary>
	Task<string> VersionAsync(CancellationToken cancellationToken = default);

	/// <summary>Initialises a repository in the working directory (<c>git init</c>).</summary>
	Task InitAsync(bool bare = false, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the porcelain working-tree status (<c>git status --porcelain=v1</c>) parsed into
	/// <see cref="GitStatusEntry"/> values. An empty list means a clean working tree.
	/// </summary>
	Task<IReadOnlyList<GitStatusEntry>> StatusAsync(CancellationToken cancellationToken = default);

	/// <summary>Stages the given paths (<c>git add &lt;paths&gt;</c>).</summary>
	Task StageAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a commit with <paramref name="message"/> (<c>git commit -m</c>) and returns the new
	/// commit's full hash. Set <paramref name="all"/> to stage tracked modifications first (<c>-a</c>).
	/// </summary>
	Task<string> CommitAsync(string message, bool all = false, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns commits reachable from <c>HEAD</c> (<c>git log</c>), newest first, optionally capped to
	/// <paramref name="maxCount"/>.
	/// </summary>
	Task<IReadOnlyList<GitCommit>> LogAsync(int? maxCount = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the current branch name (<c>git branch --show-current</c>), or an empty string when
	/// <c>HEAD</c> is detached.
	/// </summary>
	Task<string> CurrentBranchAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the local branches (<c>git branch</c>) parsed into <see cref="GitBranch"/> values, with
	/// the checked-out branch flagged via <see cref="GitBranch.IsCurrent"/>.
	/// </summary>
	Task<IReadOnlyList<GitBranch>> BranchesAsync(CancellationToken cancellationToken = default);

	/// <summary>Resolves a revision to its full commit hash (<c>git rev-parse &lt;revision&gt;</c>).</summary>
	Task<string> RevParseAsync(string revision, CancellationToken cancellationToken = default);

	/// <summary>Creates a branch pointing at <c>HEAD</c> (<c>git branch &lt;name&gt;</c>).</summary>
	Task CreateBranchAsync(string name, CancellationToken cancellationToken = default);

	/// <summary>Checks out a branch, tag or commit (<c>git checkout &lt;ref&gt;</c>).</summary>
	Task CheckoutAsync(string reference, CancellationToken cancellationToken = default);
}
