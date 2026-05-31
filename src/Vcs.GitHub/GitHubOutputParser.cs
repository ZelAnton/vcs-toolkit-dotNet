using System.Text.Json;

namespace Vcs.GitHub;

/// <summary>
/// Pure parsers for the <c>--json</c> output of the GitHub CLI. No process execution, so they are
/// unit-testable in isolation. Missing or wrong-typed fields degrade to empty/zero rather than throwing.
/// </summary>
internal static class GitHubOutputParser
{
	/// <summary>Parses a <c>gh pr list --json</c> array into <see cref="GitHubPullRequest"/> values.</summary>
	internal static IReadOnlyList<GitHubPullRequest> ParsePullRequests(string json)
	{
		using var document = JsonDocument.Parse(json);
		var pullRequests = new List<GitHubPullRequest>();
		foreach (var element in document.RootElement.EnumerateArray())
			pullRequests.Add(ReadPullRequest(element));
		return pullRequests;
	}

	/// <summary>Parses a single <c>gh pr view --json</c> object into a <see cref="GitHubPullRequest"/>.</summary>
	internal static GitHubPullRequest ParsePullRequest(string json)
	{
		using var document = JsonDocument.Parse(json);
		return ReadPullRequest(document.RootElement);
	}

	/// <summary>Parses a <c>gh repo view --json</c> object into a <see cref="GitHubRepository"/>.</summary>
	internal static GitHubRepository ParseRepository(string json)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		var owner = root.TryGetProperty("owner", out var ownerElement) && ownerElement.ValueKind == JsonValueKind.Object
			? ReadString(ownerElement, "login")
			: string.Empty;
		var defaultBranch = root.TryGetProperty("defaultBranchRef", out var branchElement) && branchElement.ValueKind == JsonValueKind.Object
			? ReadString(branchElement, "name")
			: string.Empty;

		string? description = root.TryGetProperty("description", out var descElement) && descElement.ValueKind == JsonValueKind.String
			? descElement.GetString()
			: null;
		if (string.IsNullOrEmpty(description))
			description = null;

		return new GitHubRepository(
			ReadString(root, "name"),
			owner,
			description,
			ReadString(root, "url"),
			root.TryGetProperty("isPrivate", out var privateElement) && privateElement.ValueKind == JsonValueKind.True,
			defaultBranch);
	}

	private static GitHubPullRequest ReadPullRequest(JsonElement element) => new(
		element.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.Number ? number.GetInt32() : 0,
		ReadString(element, "title"),
		ReadString(element, "state"),
		ReadString(element, "headRefName"),
		ReadString(element, "baseRefName"),
		ReadString(element, "url"));

	private static string ReadString(JsonElement element, string propertyName)
		=> element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;
}
