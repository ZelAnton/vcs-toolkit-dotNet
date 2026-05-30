namespace Vcs.Git;

/// <summary>
/// Outcome of a single <c>git</c> invocation: its captured stdout, stderr and raw exit code.
/// Returned by <see cref="GitCli.RunRawAsync"/>, which never throws on a non-zero exit.
/// </summary>
/// <param name="StdOut">Standard output, decoded as UTF-8 (not trimmed).</param>
/// <param name="StdErr">Standard error, decoded as UTF-8.</param>
/// <param name="ExitCode">The raw process exit code.</param>
public readonly record struct GitCommandResult(string StdOut, string StdErr, int ExitCode)
{
	/// <summary><c>true</c> when <see cref="ExitCode"/> is zero.</summary>
	public bool IsSuccess => ExitCode == 0;

	/// <summary>
	/// <c>true</c> when the process was killed because the configured timeout elapsed. The
	/// <see cref="ExitCode"/> in that case is the killed process's code and <see cref="IsSuccess"/>
	/// is normally <c>false</c>.
	/// </summary>
	public bool WasTimedOut { get; init; }
}
