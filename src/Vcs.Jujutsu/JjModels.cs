namespace Vcs.Jujutsu;

/// <summary>A single change as reported by <see cref="JujutsuCli.LogAsync"/>.</summary>
/// <param name="ChangeId">The stable change id.</param>
/// <param name="CommitId">The underlying Git commit id.</param>
/// <param name="Description">First line of the change description (empty when undescribed).</param>
/// <param name="Empty"><c>true</c> when the change makes no file modifications.</param>
public sealed record JjChange(string ChangeId, string CommitId, string Description, bool Empty);
