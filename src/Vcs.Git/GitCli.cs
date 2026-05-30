using System.Globalization;

namespace Vcs.Git;

/// <summary>
/// Async wrapper over the <c>git</c> command-line tool. Exposes a small set of typed commands plus
/// a raw escape hatch (<see cref="RunRawAsync"/> / <see cref="RunAsync(IEnumerable{string}, CancellationToken)"/>)
/// for anything not yet modelled. Requires <c>git</c> on <c>PATH</c> (or an explicit executable path).
/// </summary>
public sealed class GitCli : IGitCli
{
	// ASCII unit/record separators — used as field/record delimiters in `git log` pretty formats so
	// they survive arbitrary subject text that could contain spaces, tabs or newlines.
	private const char FieldSeparator = '';
	private const char RecordSeparator = '';

	private readonly ICommandExecutor _executor;
	private readonly TimeSpan? _defaultTimeout;

	/// <summary>
	/// Creates a client that drives <paramref name="executable"/> (default <c>git</c>) with
	/// <paramref name="workingDirectory"/> as the process working directory (default: the current
	/// directory of the host process). <paramref name="defaultTimeout"/>, when set, kills any command
	/// that runs longer than it; individual calls can override it via the <see cref="TimeSpan"/>
	/// overloads of <see cref="RunAsync(IEnumerable{string}, TimeSpan, CancellationToken)"/> /
	/// <see cref="RunRawAsync(IEnumerable{string}, TimeSpan, CancellationToken)"/>.
	/// </summary>
	public GitCli(string? workingDirectory = null, string executable = "git", TimeSpan? defaultTimeout = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(executable);
		ValidateTimeout(defaultTimeout, nameof(defaultTimeout));
		Executable = executable;
		WorkingDirectory = workingDirectory;
		_defaultTimeout = defaultTimeout;
		_executor = new ProcessKitCommandExecutor(executable, workingDirectory);
	}

	internal GitCli(ICommandExecutor executor, string executable = "git", string? workingDirectory = null, TimeSpan? defaultTimeout = null)
	{
		_executor = executor;
		Executable = executable;
		WorkingDirectory = workingDirectory;
		_defaultTimeout = defaultTimeout;
	}

	/// <summary>The underlying executable this client invokes.</summary>
	public string Executable { get; }

	/// <summary>The process working directory, or <c>null</c> to inherit the host process's.</summary>
	public string? WorkingDirectory { get; }

	/// <summary>The timeout applied to commands that do not specify their own, or <c>null</c> for none.</summary>
	public TimeSpan? DefaultTimeout => _defaultTimeout;

	/// <summary>
	/// Runs <c>git</c> with the given arguments (using <see cref="DefaultTimeout"/>) and returns the
	/// full result without throwing on a non-zero exit code. Use this for commands not covered by a
	/// typed wrapper.
	/// </summary>
	public Task<GitCommandResult> RunRawAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
		=> RunRawCore(arguments, _defaultTimeout, cancellationToken);

	/// <summary>
	/// Runs <c>git</c> with the given arguments, killing it after <paramref name="timeout"/>, and
	/// returns the full result without throwing on a non-zero exit code (a timeout is reported via
	/// <see cref="GitCommandResult.WasTimedOut"/>).
	/// </summary>
	public Task<GitCommandResult> RunRawAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> RunRawCore(arguments, timeout, cancellationToken);

	/// <summary>
	/// Runs <c>git</c> with the given arguments (using <see cref="DefaultTimeout"/>), throwing
	/// <see cref="GitCliException"/> on a non-zero exit code, and returns the trimmed stdout on success.
	/// </summary>
	public Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
		=> RunCore(arguments, _defaultTimeout, cancellationToken);

	/// <summary>
	/// Runs <c>git</c> with the given arguments, killing it after <paramref name="timeout"/>, throwing
	/// <see cref="GitCliException"/> on a non-zero exit (including a timeout, where
	/// <see cref="GitCliException.TimedOut"/> is <c>true</c>), and returns the trimmed stdout on success.
	/// </summary>
	public Task<string> RunAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> RunCore(arguments, timeout, cancellationToken);

	private Task<GitCommandResult> RunRawCore(IEnumerable<string> arguments, TimeSpan? timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		ValidateTimeout(timeout, nameof(timeout));
		return _executor.RunAsync(AsList(arguments), timeout, cancellationToken);
	}

	private async Task<string> RunCore(IEnumerable<string> arguments, TimeSpan? timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		ValidateTimeout(timeout, nameof(timeout));
		var args = AsList(arguments);
		var result = await _executor.RunAsync(args, timeout, cancellationToken).ConfigureAwait(false);
		if (!result.IsSuccess)
		{
			var joined = string.Join(' ', args);
			var reason = result.WasTimedOut
				? $"timed out after {timeout?.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s"
				: $"exited with code {result.ExitCode}";
			throw new GitCliException(result.ExitCode, result.StdErr, joined, result.WasTimedOut,
				$"`{Executable} {joined}` {reason}. {Truncate(result.StdErr.Trim())}");
		}

		return result.StdOut.Trim();
	}

	/// <summary>Returns the installed Git version string (<c>git --version</c>).</summary>
	public Task<string> VersionAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["--version"], cancellationToken);

	/// <summary>Initialises a repository in the working directory (<c>git init</c>).</summary>
	public Task InitAsync(bool bare = false, CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "init" };
		if (bare)
			args.Add("--bare");
		return RunAsync(args, cancellationToken);
	}

	/// <summary>
	/// Returns the porcelain working-tree status (<c>git status --porcelain=v1</c>) parsed into
	/// <see cref="GitStatusEntry"/> values. An empty list means a clean working tree.
	/// </summary>
	public async Task<IReadOnlyList<GitStatusEntry>> StatusAsync(CancellationToken cancellationToken = default)
	{
		var output = await RunAsync(["status", "--porcelain=v1"], cancellationToken).ConfigureAwait(false);
		if (output.Length == 0)
			return [];

		var entries = new List<GitStatusEntry>();
		foreach (var rawLine in output.Split('\n'))
		{
			var line = rawLine.TrimEnd('\r');
			if (line.Length < 4)
				continue;

			var path = line[3..];
			// Renames/copies are reported as "orig -> dest"; keep the destination path.
			var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
			if (arrow >= 0)
				path = path[(arrow + 4)..];

			entries.Add(new GitStatusEntry(line[0], line[1], path));
		}

		return entries;
	}

	/// <summary>Stages the given paths (<c>git add &lt;paths&gt;</c>).</summary>
	public Task StageAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(paths);
		// `--` terminates option parsing so a path that looks like a flag (e.g. "-f") is treated as a path.
		var args = new List<string> { "add", "--" };
		args.AddRange(paths);
		return RunAsync(args, cancellationToken);
	}

	/// <summary>
	/// Creates a commit with <paramref name="message"/> (<c>git commit -m</c>) and returns the new
	/// commit's full hash. Set <paramref name="all"/> to stage tracked modifications first (<c>-a</c>).
	/// </summary>
	public async Task<string> CommitAsync(string message, bool all = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);
		var args = new List<string> { "commit", "-m", message };
		if (all)
			args.Add("-a");
		await RunAsync(args, cancellationToken).ConfigureAwait(false);
		return await RunAsync(["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Returns commits reachable from <c>HEAD</c> (<c>git log</c>), newest first, optionally capped to
	/// <paramref name="maxCount"/>.
	/// </summary>
	public async Task<IReadOnlyList<GitCommit>> LogAsync(int? maxCount = null, CancellationToken cancellationToken = default)
	{
		var format = $"--pretty=format:%H{FieldSeparator}%h{FieldSeparator}%an{FieldSeparator}%aI{FieldSeparator}%s{RecordSeparator}";
		var args = new List<string> { "log", format };
		if (maxCount is { } count)
		{
			args.Add("-n");
			args.Add(count.ToString(CultureInfo.InvariantCulture));
		}

		var output = await RunAsync(args, cancellationToken).ConfigureAwait(false);
		if (output.Length == 0)
			return [];

		var commits = new List<GitCommit>();
		foreach (var record in output.Split(RecordSeparator))
		{
			var trimmed = record.Trim('\n', '\r');
			if (trimmed.Length == 0)
				continue;

			var fields = trimmed.Split(FieldSeparator);
			if (fields.Length < 5)
				continue;

			var date = DateTimeOffset.Parse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			commits.Add(new GitCommit(fields[0], fields[1], fields[2], date, fields[4]));
		}

		return commits;
	}

	/// <summary>
	/// Returns the current branch name (<c>git branch --show-current</c>), or an empty string when
	/// <c>HEAD</c> is detached.
	/// </summary>
	public Task<string> CurrentBranchAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["branch", "--show-current"], cancellationToken);

	/// <summary>Creates a branch pointing at <c>HEAD</c> (<c>git branch &lt;name&gt;</c>).</summary>
	public Task CreateBranchAsync(string name, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		return RunAsync(["branch", name], cancellationToken);
	}

	/// <summary>Checks out a branch, tag or commit (<c>git checkout &lt;ref&gt;</c>).</summary>
	public Task CheckoutAsync(string reference, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(reference);
		return RunAsync(["checkout", reference], cancellationToken);
	}

	private static IReadOnlyList<string> AsList(IEnumerable<string> arguments)
		=> arguments as IReadOnlyList<string> ?? arguments.ToList();

	private static string Truncate(string text)
	{
		const int max = 4096;
		return text.Length > max ? string.Concat(text.AsSpan(0, max), "...[truncated]") : text;
	}

	private static void ValidateTimeout(TimeSpan? timeout, string parameterName)
	{
		if (timeout is { } value && value <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(parameterName, value, "Timeout must be positive.");
	}
}
