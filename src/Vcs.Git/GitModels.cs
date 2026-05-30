namespace Vcs.Git;

/// <summary>A single commit as reported by <see cref="GitCli.LogAsync"/>.</summary>
/// <param name="Hash">Full 40-character commit hash (<c>%H</c>).</param>
/// <param name="ShortHash">Abbreviated commit hash (<c>%h</c>).</param>
/// <param name="Author">Author name (<c>%an</c>).</param>
/// <param name="Date">Author date (<c>%aI</c>, strict ISO 8601).</param>
/// <param name="Subject">Commit subject line (<c>%s</c>).</param>
public sealed record GitCommit(string Hash, string ShortHash, string Author, DateTimeOffset Date, string Subject);

/// <summary>
/// One entry of <c>git status --porcelain=v1</c> as reported by <see cref="GitCli.StatusAsync"/>.
/// </summary>
/// <param name="Index">Status code of the staged (index) side; a space means unmodified.</param>
/// <param name="WorkTree">Status code of the unstaged (work-tree) side; a space means unmodified.</param>
/// <param name="Path">Path of the changed entry (the destination path for renames/copies).</param>
public sealed record GitStatusEntry(char Index, char WorkTree, string Path);
