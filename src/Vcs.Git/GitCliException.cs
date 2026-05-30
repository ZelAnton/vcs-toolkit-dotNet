namespace Vcs.Git;

/// <summary>
/// Thrown when a <c>git</c> command run through <see cref="GitCli.RunAsync"/> (or a typed wrapper
/// built on it) exits with a non-zero code. Carries the exit code, captured stderr and the
/// argument string for diagnostics.
/// </summary>
public sealed class GitCliException : Exception
{
	internal GitCliException(int exitCode, string stdErr, string arguments, string message)
		: base(message)
	{
		ExitCode = exitCode;
		StdErr = stdErr;
		Arguments = arguments;
	}

	/// <summary>The raw process exit code.</summary>
	public int ExitCode { get; }

	/// <summary>The captured stderr of the failed command.</summary>
	public string StdErr { get; }

	/// <summary>The space-joined arguments passed to <c>git</c>.</summary>
	public string Arguments { get; }
}
