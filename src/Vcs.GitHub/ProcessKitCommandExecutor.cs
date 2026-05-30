using System.ComponentModel;
using System.Diagnostics;
using ProcessKit;

namespace Vcs.GitHub;

/// <summary>
/// Default <see cref="ICommandExecutor"/> backed by ProcessKit. Keeps ProcessKit out of the public
/// surface — callers see only <see cref="GitHubCommandResult"/>.
/// </summary>
internal sealed class ProcessKitCommandExecutor : ICommandExecutor
{
	private readonly IProcessRunner _runner;
	private readonly string _executable;
	private readonly string? _workingDirectory;

	internal ProcessKitCommandExecutor(string executable, string? workingDirectory, IProcessRunner? runner = null)
	{
		_executable = executable;
		_workingDirectory = workingDirectory;
		_runner = runner ?? ProcessRunner.Default;
	}

	public async Task<GitHubCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout, CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo(_executable);
		foreach (var argument in arguments)
			startInfo.ArgumentList.Add(argument);
		if (_workingDirectory is not null)
			startInfo.WorkingDirectory = _workingDirectory;

		var options = timeout is { } value ? new ProcessRunOptions { Timeout = value } : null;

		ProcessResult<string> result;
		try
		{
			result = await _runner.GetFullOutputAsync(startInfo, options, cancellationToken).ConfigureAwait(false);
		}
		catch (Win32Exception ex)
		{
			// The process could not be started at all (executable missing or not executable). Surface
			// it as the library's own exception instead of leaking the raw Win32Exception to callers.
			var joined = string.Join(' ', arguments);
			throw new GitHubCliException(-1, ex.Message, joined, false,
				$"Could not start `{_executable}`: {ex.Message}", ex);
		}

		return new GitHubCommandResult(result.StdOut, result.StdErr, result.ExitCode) { WasTimedOut = result.WasTimedOut };
	}
}
