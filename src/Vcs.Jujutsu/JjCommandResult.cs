namespace Vcs.Jujutsu;

/// <summary>
/// Outcome of a single <c>jj</c> invocation: its captured stdout, stderr and raw exit code.
/// Returned by <see cref="JujutsuCli.RunRawAsync"/>, which never throws on a non-zero exit.
/// </summary>
/// <param name="StdOut">Standard output, decoded as UTF-8 (not trimmed).</param>
/// <param name="StdErr">Standard error, decoded as UTF-8.</param>
/// <param name="ExitCode">The raw process exit code.</param>
public readonly record struct JjCommandResult(string StdOut, string StdErr, int ExitCode)
{
	/// <summary><c>true</c> when <see cref="ExitCode"/> is zero.</summary>
	public bool IsSuccess => ExitCode == 0;
}
