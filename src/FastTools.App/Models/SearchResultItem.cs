using System.Windows.Media;

namespace FastTools.App.Models;

public sealed class SearchResultItem
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string Group { get; init; }

    public string DisplayGroup { get; set; } = string.Empty;

    public string Glyph { get; init; } = "\uE721";

    public ImageSource? Icon { get; init; }

    public double Score { get; set; }

    public bool RequiresConfirmation { get; init; }

    public string? ConfirmationMessage { get; init; }

    public bool SupportsRunAsAdmin { get; init; }

    public required Func<CancellationToken, Task> ExecuteAsync { get; init; }

    public Func<CancellationToken, Task>? ExecuteAsAdminAsync { get; init; }

    public IReadOnlyList<SearchAction> Actions { get; init; } = [];
}
