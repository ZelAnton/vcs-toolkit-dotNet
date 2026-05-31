namespace Vcs.Jujutsu;

/// <summary>
/// Abstraction over the <c>jj</c> (Jujutsu) command-line client implemented by
/// <see cref="JujutsuCli"/>. Depend on this interface (rather than the concrete class) to keep call
/// sites testable — it can be substituted with a mock/fake in unit tests (e.g.
/// <c>Substitute.For&lt;IJujutsuCli&gt;()</c> with NSubstitute, or <c>new Mock&lt;IJujutsuCli&gt;()</c>
/// with Moq). Every public operation of <see cref="JujutsuCli"/> is exposed here; construction is
/// done via <see cref="JujutsuCli"/>'s constructors.
/// </summary>
public interface IJujutsuCli
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
	/// Runs <c>jj</c> with the given arguments (using <see cref="DefaultTimeout"/>) and returns the
	/// full result without throwing on a non-zero exit code.
	/// </summary>
	Task<JjCommandResult> RunRawAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>jj</c> with the given arguments, killing it after <paramref name="timeout"/>, and
	/// returns the full result without throwing on a non-zero exit code (a timeout is reported via
	/// <see cref="JjCommandResult.WasTimedOut"/>).
	/// </summary>
	Task<JjCommandResult> RunRawAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>jj</c> with the given arguments, piping <paramref name="standardInput"/> to stdin
	/// (optionally killing it after <paramref name="timeout"/>, else <see cref="DefaultTimeout"/>), and
	/// returns the full result without throwing on a non-zero exit code.
	/// </summary>
	Task<JjCommandResult> RunRawAsync(IEnumerable<string> arguments, string standardInput, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>jj</c> with the given arguments (using <see cref="DefaultTimeout"/>), throwing
	/// <see cref="JujutsuCliException"/> on a non-zero exit code, and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>jj</c> with the given arguments, killing it after <paramref name="timeout"/>, throwing
	/// <see cref="JujutsuCliException"/> on a non-zero exit (including a timeout, where
	/// <see cref="JujutsuCliException.TimedOut"/> is <c>true</c>), and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs <c>jj</c> with the given arguments, piping <paramref name="standardInput"/> to stdin
	/// (optionally killing it after <paramref name="timeout"/>, else <see cref="DefaultTimeout"/>),
	/// throwing <see cref="JujutsuCliException"/> on a non-zero exit, and returns the trimmed stdout on success.
	/// </summary>
	Task<string> RunAsync(IEnumerable<string> arguments, string standardInput, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

	/// <summary>Returns the installed Jujutsu version string (<c>jj --version</c>).</summary>
	Task<string> VersionAsync(CancellationToken cancellationToken = default);

	/// <summary>Returns the working-copy status as printed by <c>jj status</c>.</summary>
	Task<string> StatusAsync(CancellationToken cancellationToken = default);

	/// <summary>Sets the description of a change (<c>jj describe</c>; defaults to <c>@</c>).</summary>
	Task DescribeAsync(string message, string revision = "@", CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a new change (<c>jj new</c>) on top of <paramref name="parents"/> (defaults to <c>@</c>
	/// when none are given), optionally with <paramref name="message"/> as its description.
	/// </summary>
	Task NewAsync(string? message = null, IEnumerable<string>? parents = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns changes (<c>jj log</c>) parsed into <see cref="JjChange"/> values, optionally filtered
	/// by <paramref name="revset"/> and capped to <paramref name="limit"/>.
	/// </summary>
	Task<IReadOnlyList<JjChange>> LogAsync(int? limit = null, string? revset = null, CancellationToken cancellationToken = default);

	/// <summary>Lists bookmarks as printed by <c>jj bookmark list</c>.</summary>
	Task<string> BookmarkListAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates or moves a bookmark to a revision (<c>jj bookmark set</c>; defaults to <c>@</c>).
	/// </summary>
	Task BookmarkSetAsync(string name, string revision = "@", CancellationToken cancellationToken = default);

	/// <summary>Fetches from the Git remote (<c>jj git fetch</c>).</summary>
	Task GitFetchAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Pushes to the Git remote (<c>jj git push</c>), optionally restricted to a single
	/// <paramref name="bookmark"/>.
	/// </summary>
	Task GitPushAsync(string? bookmark = null, CancellationToken cancellationToken = default);
}
