namespace Vcs.Jujutsu;

/// <summary>
/// Pure parser for <c>jj log</c> output. No process execution, so it is unit-testable in isolation.
/// The <see cref="LogTemplate"/> emits the textual escapes <c>\x1f</c> / <c>\x1e</c> (jj interprets
/// them into the real control characters); the parser then splits on those control characters, which
/// survive arbitrary description text.
/// </summary>
internal static class JjOutputParser
{
	internal const char UnitSeparator = '';
	internal const char RecordSeparator = '';

	/// <summary><c>jj log</c> template producing one <see cref="JjChange"/> per record.</summary>
	internal const string LogTemplate =
		"change_id ++ \"\\x1f\" ++ commit_id ++ \"\\x1f\" ++ if(empty, \"true\", \"false\") ++ \"\\x1f\" ++ description.first_line() ++ \"\\x1e\"";

	/// <summary>Parses the <see cref="LogTemplate"/> output of <c>jj log</c>. Empty input yields an empty list.</summary>
	internal static IReadOnlyList<JjChange> ParseChanges(string output)
	{
		if (output.Length == 0)
			return [];

		var changes = new List<JjChange>();
		foreach (var record in output.Split(RecordSeparator))
		{
			var trimmed = record.Trim('\n', '\r');
			if (trimmed.Length == 0)
				continue;

			var fields = trimmed.Split(UnitSeparator);
			if (fields.Length < 4)
				continue;

			var empty = string.Equals(fields[2], "true", StringComparison.Ordinal);
			changes.Add(new JjChange(fields[0], fields[1], fields[3], empty));
		}

		return changes;
	}
}
