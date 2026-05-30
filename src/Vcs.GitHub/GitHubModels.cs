namespace Vcs.GitHub;

/// <summary>A pull request as reported by <see cref="GitHubCli.PrListAsync"/> / <see cref="GitHubCli.PrViewAsync"/>.</summary>
/// <param name="Number">The pull request number.</param>
/// <param name="Title">The pull request title.</param>
/// <param name="State">State as reported by <c>gh</c> (e.g. <c>OPEN</c>, <c>MERGED</c>, <c>CLOSED</c>).</param>
/// <param name="HeadRefName">The head (source) branch name.</param>
/// <param name="BaseRefName">The base (target) branch name.</param>
/// <param name="Url">The pull request web URL.</param>
public sealed record GitHubPullRequest(int Number, string Title, string State, string HeadRefName, string BaseRefName, string Url);

/// <summary>A repository as reported by <see cref="GitHubCli.RepoViewAsync"/>.</summary>
/// <param name="Name">The repository name.</param>
/// <param name="Owner">The owner login.</param>
/// <param name="Description">The repository description, or <c>null</c> when unset.</param>
/// <param name="Url">The repository web URL.</param>
/// <param name="IsPrivate"><c>true</c> for a private repository.</param>
/// <param name="DefaultBranch">The default branch name, or an empty string for an empty repository.</param>
public sealed record GitHubRepository(string Name, string Owner, string? Description, string Url, bool IsPrivate, string DefaultBranch);
