namespace Vcs.Git;

/// <summary>
/// Thrown when a <c>git</c> command run through <see cref="GitCli.RunAsync"/> (or a typed wrapper
/// built on it) exits with a non-zero code. Carries the exit code, captured stderr and the
/// argument string for diagnostics.
/// </summary>
public sealed class GitCliException : Exception
{
	/// <summary>
	/// Creates an exception describing a failed <c>git</c> invocation. Public so consumers can
	/// construct it in tests/mocks (e.g. to make a mocked <see cref="IGitCli"/> throw).
	/// </summary>
	public GitCliException(string message, int exitCode = 0, string stdErr = "", string arguments = "", bool timedOut = false)
		: this(exitCode, stdErr, arguments, timedOut, message)
	{
	}

	internal GitCliException(int exitCode, string stdErr, string arguments, bool timedOut, string message, Exception? innerException = null)
		: base(message, innerException)
	{
		ExitCode = exitCode;
		StdErr = stdErr;
		Arguments = arguments;
		TimedOut = timedOut;
	}

	/// <summary>The raw process exit code.</summary>
	public int ExitCode { get; }

	/// <summary>The captured stderr of the failed command.</summary>
	public string StdErr { get; }

	/// <summary>The space-joined arguments passed to <c>git</c>.</summary>
	public string Arguments { get; }

	/// <summary><c>true</c> when the command was killed because its timeout elapsed.</summary>
	public bool TimedOut { get; }
}
