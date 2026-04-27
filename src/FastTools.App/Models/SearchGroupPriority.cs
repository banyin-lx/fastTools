namespace FastTools.App.Models;

public sealed class SearchGroupPriority
{
    public string Group { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsEnabled { get; set; } = true;
}
