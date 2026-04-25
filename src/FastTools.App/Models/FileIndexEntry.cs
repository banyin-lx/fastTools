namespace FastTools.App.Models;

public sealed class FileIndexEntry
{
    public required string FullPath { get; init; }

    public required string Name { get; init; }

    public required bool IsDirectory { get; init; }

    public string ParentDirectory { get; init; } = string.Empty;

    public string NormalizedName { get; init; } = string.Empty;

    public string NormalizedParentDirectory { get; init; } = string.Empty;
}
