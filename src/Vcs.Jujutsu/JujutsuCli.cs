using System.Globalization;

namespace Vcs.Jujutsu;

/// <summary>
/// Async wrapper over the <c>jj</c> (Jujutsu) command-line tool. Exposes a small set of typed
/// commands plus a raw escape hatch (<see cref="RunRawAsync"/> /
/// <see cref="RunAsync(IEnumerable{string}, CancellationToken)"/>) for anything not yet modelled.
/// Requires <c>jj</c> on <c>PATH</c> (or an explicit executable path).
/// </summary>
public sealed class JujutsuCli
{
	// Field/record delimiters used in the `jj log` template. The C# side splits on the real control
	// characters; the template literal sends jj the textual escapes "\x1f" / "\x1e".
	private const char FieldSeparator = '';
	private const char RecordSeparator = '';

	private const string LogTemplate =
		"change_id ++ \"\\x1f\" ++ commit_id ++ \"\\x1f\" ++ if(empty, \"true\", \"false\") ++ \"\\x1f\" ++ description.first_line() ++ \"\\x1e\"";

	private readonly ICommandExecutor _executor;

	/// <summary>
	/// Creates a client that drives <paramref name="executable"/> (default <c>jj</c>) with
	/// <paramref name="workingDirectory"/> as the process working directory (default: the current
	/// directory of the host process).
	/// </summary>
	public JujutsuCli(string? workingDirectory = null, string executable = "jj")
	{
		ArgumentException.ThrowIfNullOrEmpty(executable);
		Executable = executable;
		WorkingDirectory = workingDirectory;
		_executor = new ProcessKitCommandExecutor(executable, workingDirectory);
	}

	internal JujutsuCli(ICommandExecutor executor, string executable = "jj", string? workingDirectory = null)
	{
		_executor = executor;
		Executable = executable;
		WorkingDirectory = workingDirectory;
	}

	/// <summary>The underlying executable this client invokes.</summary>
	public string Executable { get; }

	/// <summary>The process working directory, or <c>null</c> to inherit the host process's.</summary>
	public string? WorkingDirectory { get; }

	/// <summary>
	/// Runs <c>jj</c> with the given arguments and returns the full result without throwing on a
	/// non-zero exit code. Use this for commands not covered by a typed wrapper.
	/// </summary>
	public Task<JjCommandResult> RunRawAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		return _executor.RunAsync(AsList(arguments), cancellationToken);
	}

	/// <summary>
	/// Runs <c>jj</c> with the given arguments, throwing <see cref="JujutsuCliException"/> on a
	/// non-zero exit code, and returns the trimmed stdout on success.
	/// </summary>
	public async Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		var args = AsList(arguments);
		var result = await _executor.RunAsync(args, cancellationToken).ConfigureAwait(false);
		if (!result.IsSuccess)
		{
			var joined = string.Join(' ', args);
			throw new JujutsuCliException(result.ExitCode, result.StdErr, joined,
				$"`{Executable} {joined}` exited with code {result.ExitCode}. {result.StdErr.Trim()}");
		}

		return result.StdOut.Trim();
	}

	/// <summary>Returns the installed Jujutsu version string (<c>jj --version</c>).</summary>
	public Task<string> VersionAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["--version"], cancellationToken);

	/// <summary>Returns the working-copy status as printed by <c>jj status</c>.</summary>
	public Task<string> StatusAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["status"], cancellationToken);

	/// <summary>Sets the description of a change (<c>jj describe</c>; defaults to <c>@</c>).</summary>
	public Task DescribeAsync(string message, string revision = "@", CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrEmpty(revision);
		return RunAsync(["describe", "-r", revision, "-m", message], cancellationToken);
	}

	/// <summary>
	/// Creates a new change (<c>jj new</c>) on top of <paramref name="parents"/> (defaults to <c>@</c>
	/// when none are given), optionally with <paramref name="message"/> as its description.
	/// </summary>
	public Task NewAsync(string? message = null, IEnumerable<string>? parents = null, CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "new" };
		if (parents is not null)
			args.AddRange(parents);
		if (message is not null)
		{
			args.Add("-m");
			args.Add(message);
		}

		return RunAsync(args, cancellationToken);
	}

	/// <summary>
	/// Returns changes (<c>jj log</c>) parsed into <see cref="JjChange"/> values, optionally filtered
	/// by <paramref name="revset"/> and capped to <paramref name="limit"/>.
	/// </summary>
	public async Task<IReadOnlyList<JjChange>> LogAsync(int? limit = null, string? revset = null, CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "log", "--no-graph", "-T", LogTemplate };
		if (revset is not null)
		{
			args.Add("-r");
			args.Add(revset);
		}

		if (limit is { } count)
		{
			args.Add("-n");
			args.Add(count.ToString(CultureInfo.InvariantCulture));
		}

		var output = await RunAsync(args, cancellationToken).ConfigureAwait(false);
		if (output.Length == 0)
			return [];

		var changes = new List<JjChange>();
		foreach (var record in output.Split(RecordSeparator))
		{
			var trimmed = record.Trim('\n', '\r');
			if (trimmed.Length == 0)
				continue;

			var fields = trimmed.Split(FieldSeparator);
			if (fields.Length < 4)
				continue;

			var empty = string.Equals(fields[2], "true", StringComparison.Ordinal);
			changes.Add(new JjChange(fields[0], fields[1], fields[3], empty));
		}

		return changes;
	}

	/// <summary>Lists bookmarks as printed by <c>jj bookmark list</c>.</summary>
	public Task<string> BookmarkListAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["bookmark", "list"], cancellationToken);

	/// <summary>
	/// Creates or moves a bookmark to a revision (<c>jj bookmark set</c>; defaults to <c>@</c>).
	/// </summary>
	public Task BookmarkSetAsync(string name, string revision = "@", CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentException.ThrowIfNullOrEmpty(revision);
		return RunAsync(["bookmark", "set", name, "-r", revision], cancellationToken);
	}

	/// <summary>Fetches from the Git remote (<c>jj git fetch</c>).</summary>
	public Task GitFetchAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["git", "fetch"], cancellationToken);

	/// <summary>
	/// Pushes to the Git remote (<c>jj git push</c>), optionally restricted to a single
	/// <paramref name="bookmark"/>.
	/// </summary>
	public Task GitPushAsync(string? bookmark = null, CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "git", "push" };
		if (bookmark is not null)
		{
			args.Add("--bookmark");
			args.Add(bookmark);
		}

		return RunAsync(args, cancellationToken);
	}

	private static IReadOnlyList<string> AsList(IEnumerable<string> arguments)
		=> arguments as IReadOnlyList<string> ?? arguments.ToList();
}
