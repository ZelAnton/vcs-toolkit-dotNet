using System.Diagnostics;
using ProcessKit;

namespace Vcs.Git;

/// <summary>
/// Default <see cref="ICommandExecutor"/> backed by ProcessKit. Keeps ProcessKit out of the public
/// surface — callers see only <see cref="GitCommandResult"/>.
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

	public async Task<GitCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo(_executable);
		foreach (var argument in arguments)
			startInfo.ArgumentList.Add(argument);
		if (_workingDirectory is not null)
			startInfo.WorkingDirectory = _workingDirectory;

		var result = await _runner.GetFullOutputAsync(startInfo, cancellationToken: cancellationToken).ConfigureAwait(false);
		return new GitCommandResult(result.StdOut, result.StdErr, result.ExitCode);
	}
}
