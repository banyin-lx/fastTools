namespace FastTools.App.Models;

public sealed class SearchAction
{
    public required string Label { get; init; }

    public string Shortcut { get; init; } = string.Empty;

    public required Func<Task> ExecuteAsync { get; init; }
}
