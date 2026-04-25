namespace FastTools.App.Models;

public sealed class PluginState
{
    public string PluginId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
