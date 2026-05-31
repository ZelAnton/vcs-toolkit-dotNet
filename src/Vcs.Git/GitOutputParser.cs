using System.Globalization;

namespace Vcs.Git;

/// <summary>
/// Pure parsers for git's machine-readable output. No process execution, so these are unit-testable
/// in isolation. The ASCII unit/record separators are used as field/record delimiters in the
/// <c>git log</c> pretty format so they survive arbitrary subject text (spaces, tabs, newlines).
/// </summary>
internal static class GitOutputParser
{
	internal const char UnitSeparator = '';
	internal const char RecordSeparator = '';

	/// <summary><c>git log</c> pretty format producing one <see cref="GitCommit"/> per record.</summary>
	internal const string LogFormat = "--pretty=format:%H%h%an%aI%s";

	/// <summary>Parses <c>git status --porcelain=v1</c> output. Empty input yields an empty list.</summary>
	internal static IReadOnlyList<GitStatusEntry> ParseStatus(string output)
	{
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

	/// <summary>Parses the <see cref="LogFormat"/> output of <c>git log</c>. Empty input yields an empty list.</summary>
	internal static IReadOnlyList<GitCommit> ParseLog(string output)
	{
		if (output.Length == 0)
			return [];

		var commits = new List<GitCommit>();
		foreach (var record in output.Split(RecordSeparator))
		{
			var trimmed = record.Trim('\n', '\r');
			if (trimmed.Length == 0)
				continue;

			var fields = trimmed.Split(UnitSeparator);
			if (fields.Length < 5)
				continue;

			var date = DateTimeOffset.Parse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			commits.Add(new GitCommit(fields[0], fields[1], fields[2], date, fields[4]));
		}

		return commits;
	}

	/// <summary>
	/// Parses <c>git branch</c> output. The first column is the marker (<c>* </c> for the current
	/// branch). The detached-HEAD pseudo-entry (<c>(HEAD detached at …)</c>) is skipped.
	/// </summary>
	internal static IReadOnlyList<GitBranch> ParseBranches(string output)
	{
		if (output.Length == 0)
			return [];

		var branches = new List<GitBranch>();
		foreach (var rawLine in output.Split('\n'))
		{
			var line = rawLine.TrimEnd('\r');
			if (line.Trim().Length == 0)
				continue;

			var current = line.StartsWith('*');
			var name = (line.Length > 1 ? line[1..] : string.Empty).Trim();
			if (name.Length == 0 || name.StartsWith('('))
				continue;

			branches.Add(new GitBranch(name, current));
		}

		return branches;
	}
}
