using System.Globalization;

namespace Vcs.GitHub;

/// <summary>
/// Async wrapper over the <c>gh</c> (GitHub CLI) command-line tool. Exposes a small set of typed
/// commands plus a raw escape hatch (<see cref="RunRawAsync"/> /
/// <see cref="RunAsync(IEnumerable{string}, CancellationToken)"/>) for anything not yet modelled.
/// Requires <c>gh</c> on <c>PATH</c> (or an explicit executable path) and an authenticated session.
/// </summary>
public sealed class GitHubCli : IGitHubCli
{
	private const string PullRequestJsonFields = "number,title,state,headRefName,baseRefName,url";
	private const string RepositoryJsonFields = "name,owner,description,url,isPrivate,defaultBranchRef";

	private readonly ICommandExecutor _executor;
	private readonly TimeSpan? _defaultTimeout;

	/// <summary>
	/// Creates a client that drives <paramref name="executable"/> (default <c>gh</c>) with
	/// <paramref name="workingDirectory"/> as the process working directory (default: the current
	/// directory of the host process). The working directory selects the repository for
	/// repository-scoped commands. <paramref name="defaultTimeout"/>, when set, kills any command that
	/// runs longer than it; individual calls can override it via the <see cref="TimeSpan"/> overloads
	/// of <see cref="RunAsync(IEnumerable{string}, TimeSpan, CancellationToken)"/> /
	/// <see cref="RunRawAsync(IEnumerable{string}, TimeSpan, CancellationToken)"/>.
	/// </summary>
	public GitHubCli(
		string? workingDirectory = null,
		string executable = "gh",
		TimeSpan? defaultTimeout = null,
		IReadOnlyDictionary<string, string>? environment = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(executable);
		ValidateTimeout(defaultTimeout, nameof(defaultTimeout));
		Executable = executable;
		WorkingDirectory = workingDirectory;
		// Snapshot so later external mutation of the caller's dictionary can't change this client.
		Environment = environment is null ? null : new Dictionary<string, string>(environment);
		_defaultTimeout = defaultTimeout;
		_executor = new ProcessKitCommandExecutor(executable, workingDirectory, Environment);
	}

	internal GitHubCli(ICommandExecutor executor, string executable = "gh", string? workingDirectory = null, TimeSpan? defaultTimeout = null, IReadOnlyDictionary<string, string>? environment = null)
	{
		_executor = executor;
		Executable = executable;
		WorkingDirectory = workingDirectory;
		Environment = environment is null ? null : new Dictionary<string, string>(environment);
		_defaultTimeout = defaultTimeout;
	}

	/// <summary>The underlying executable this client invokes.</summary>
	public string Executable { get; }

	/// <summary>The process working directory, or <c>null</c> to inherit the host process's.</summary>
	public string? WorkingDirectory { get; }

	/// <summary>The timeout applied to commands that do not specify their own, or <c>null</c> for none.</summary>
	public TimeSpan? DefaultTimeout => _defaultTimeout;

	/// <summary>Environment variables applied (on top of the inherited environment) to every command, or <c>null</c>.</summary>
	public IReadOnlyDictionary<string, string>? Environment { get; }

	/// <summary>
	/// Runs <c>gh</c> with the given arguments (using <see cref="DefaultTimeout"/>) and returns the
	/// full result without throwing on a non-zero exit code. Use this for commands not covered by a
	/// typed wrapper.
	/// </summary>
	public Task<GitHubCommandResult> RunRawAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
		=> RunRawCore(arguments, _defaultTimeout, standardInput: null, cancellationToken);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments, killing it after <paramref name="timeout"/>, and
	/// returns the full result without throwing on a non-zero exit code (a timeout is reported via
	/// <see cref="GitHubCommandResult.WasTimedOut"/>).
	/// </summary>
	public Task<GitHubCommandResult> RunRawAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> RunRawCore(arguments, timeout, standardInput: null, cancellationToken);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments, piping <paramref name="standardInput"/> to the process's
	/// stdin (optionally killing it after <paramref name="timeout"/>, else <see cref="DefaultTimeout"/>),
	/// and returns the full result without throwing on a non-zero exit code.
	/// </summary>
	public Task<GitHubCommandResult> RunRawAsync(IEnumerable<string> arguments, string standardInput, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(standardInput);
		return RunRawCore(arguments, timeout ?? _defaultTimeout, standardInput, cancellationToken);
	}

	/// <summary>
	/// Runs <c>gh</c> with the given arguments (using <see cref="DefaultTimeout"/>), throwing
	/// <see cref="GitHubCliException"/> on a non-zero exit code, and returns the trimmed stdout on success.
	/// </summary>
	public Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
		=> RunCore(arguments, _defaultTimeout, standardInput: null, cancellationToken);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments, killing it after <paramref name="timeout"/>, throwing
	/// <see cref="GitHubCliException"/> on a non-zero exit (including a timeout, where
	/// <see cref="GitHubCliException.TimedOut"/> is <c>true</c>), and returns the trimmed stdout on success.
	/// </summary>
	public Task<string> RunAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> RunCore(arguments, timeout, standardInput: null, cancellationToken);

	/// <summary>
	/// Runs <c>gh</c> with the given arguments, piping <paramref name="standardInput"/> to the process's
	/// stdin (optionally killing it after <paramref name="timeout"/>, else <see cref="DefaultTimeout"/>),
	/// throwing <see cref="GitHubCliException"/> on a non-zero exit, and returns the trimmed stdout on success.
	/// </summary>
	public Task<string> RunAsync(IEnumerable<string> arguments, string standardInput, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(standardInput);
		return RunCore(arguments, timeout ?? _defaultTimeout, standardInput, cancellationToken);
	}

	private Task<GitHubCommandResult> RunRawCore(IEnumerable<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		ValidateTimeout(timeout, nameof(timeout));
		return _executor.RunAsync(AsList(arguments), timeout, standardInput, cancellationToken);
	}

	private async Task<string> RunCore(IEnumerable<string> arguments, TimeSpan? timeout, string? standardInput, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		ValidateTimeout(timeout, nameof(timeout));
		var args = AsList(arguments);
		var result = await _executor.RunAsync(args, timeout, standardInput, cancellationToken).ConfigureAwait(false);
		if (!result.IsSuccess)
		{
			var joined = string.Join(' ', args);
			var reason = result.WasTimedOut
				? $"timed out after {timeout?.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s"
				: $"exited with code {result.ExitCode}";
			throw new GitHubCliException(result.ExitCode, result.StdErr, joined, result.WasTimedOut,
				$"`{Executable} {joined}` {reason}. {Truncate(result.StdErr.Trim())}");
		}

		return result.StdOut.Trim();
	}

	/// <summary>Returns the installed GitHub CLI version string (<c>gh --version</c>).</summary>
	public Task<string> VersionAsync(CancellationToken cancellationToken = default)
		=> RunAsync(["--version"], cancellationToken);

	/// <summary>
	/// Returns <c>true</c> when there is an authenticated <c>gh</c> session (<c>gh auth status</c>
	/// exits zero), <c>false</c> otherwise.
	/// </summary>
	public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
	{
		var result = await RunRawAsync(["auth", "status"], cancellationToken).ConfigureAwait(false);
		return result.IsSuccess;
	}

	/// <summary>
	/// Returns metadata for a repository (<c>gh repo view --json</c>). Pass <paramref name="repository"/>
	/// as <c>OWNER/REPO</c> (or a URL); when <c>null</c>, the repository for the working directory is used.
	/// </summary>
	public async Task<GitHubRepository> RepoViewAsync(string? repository = null, CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "repo", "view" };
		if (repository is not null)
			args.Add(repository);
		args.Add("--json");
		args.Add(RepositoryJsonFields);

		var json = await RunAsync(args, cancellationToken).ConfigureAwait(false);
		return GitHubOutputParser.ParseRepository(json);
	}

	/// <summary>
	/// Lists pull requests (<c>gh pr list --json</c>), optionally filtered by
	/// <paramref name="state"/> (<c>open</c>, <c>closed</c>, <c>merged</c> or <c>all</c>). When
	/// <paramref name="limit"/> is <c>null</c>, <c>gh</c>'s own default cap applies (30 at the time of
	/// writing); pass an explicit value to raise or lower it.
	/// </summary>
	public async Task<IReadOnlyList<GitHubPullRequest>> PrListAsync(string? state = null, int? limit = null, CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "pr", "list", "--json", PullRequestJsonFields };
		if (state is not null)
		{
			args.Add("--state");
			args.Add(state);
		}

		if (limit is { } count)
		{
			args.Add("--limit");
			args.Add(count.ToString(CultureInfo.InvariantCulture));
		}

		var json = await RunAsync(args, cancellationToken).ConfigureAwait(false);
		return GitHubOutputParser.ParsePullRequests(json);
	}

	/// <summary>Returns a single pull request by number (<c>gh pr view &lt;number&gt; --json</c>).</summary>
	public async Task<GitHubPullRequest> PrViewAsync(int number, CancellationToken cancellationToken = default)
	{
		var args = new List<string>
		{
			"pr", "view", number.ToString(CultureInfo.InvariantCulture), "--json", PullRequestJsonFields,
		};

		var json = await RunAsync(args, cancellationToken).ConfigureAwait(false);
		return GitHubOutputParser.ParsePullRequest(json);
	}

	/// <summary>
	/// Creates a pull request (<c>gh pr create</c>) and returns the new pull request's URL.
	/// <paramref name="baseBranch"/> sets the target branch (defaults to the repository default).
	/// </summary>
	public Task<string> PrCreateAsync(string title, string body, string? baseBranch = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(title);
		ArgumentNullException.ThrowIfNull(body);
		var args = new List<string> { "pr", "create", "--title", title, "--body", body };
		if (baseBranch is not null)
		{
			args.Add("--base");
			args.Add(baseBranch);
		}

		return RunAsync(args, cancellationToken);
	}

	/// <summary>
	/// Calls the GitHub REST/GraphQL API through <c>gh api</c> and returns the raw response body.
	/// <paramref name="method"/> overrides the HTTP method (e.g. <c>POST</c>).
	/// </summary>
	public Task<string> ApiAsync(string endpoint, string? method = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(endpoint);
		var args = new List<string> { "api", endpoint };
		if (method is not null)
		{
			args.Add("-X");
			args.Add(method);
		}

		return RunAsync(args, cancellationToken);
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
