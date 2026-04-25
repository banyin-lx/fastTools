namespace FastTools.App.Models;

public sealed class ApplicationEntry
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string LaunchTarget { get; init; }

    public string? Arguments { get; init; }

    public string Location { get; init; } = string.Empty;

    public bool IsPackagedApp { get; init; }

    public List<string> Aliases { get; init; } = [];

    public string NormalizedName { get; init; } = string.Empty;

    public string NormalizedLocation { get; init; } = string.Empty;

    public string NormalizedAliases { get; init; } = string.Empty;
}
