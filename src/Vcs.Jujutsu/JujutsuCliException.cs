namespace Vcs.Jujutsu;

/// <summary>
/// Thrown when a <c>jj</c> command run through <see cref="JujutsuCli.RunAsync"/> (or a typed wrapper
/// built on it) exits with a non-zero code. Carries the exit code, captured stderr and the
/// argument string for diagnostics.
/// </summary>
public sealed class JujutsuCliException : Exception
{
	internal JujutsuCliException(int exitCode, string stdErr, string arguments, bool timedOut, string message)
		: base(message)
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

	/// <summary>The space-joined arguments passed to <c>jj</c>.</summary>
	public string Arguments { get; }

	/// <summary><c>true</c> when the command was killed because its timeout elapsed.</summary>
	public bool TimedOut { get; }
}
