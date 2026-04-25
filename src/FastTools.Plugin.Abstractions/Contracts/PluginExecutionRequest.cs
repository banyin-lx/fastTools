namespace FastTools.Plugin.Abstractions.Contracts;

public sealed record PluginExecutionRequest(
    PluginSearchItem Item,
    IReadOnlyDictionary<string, string> Settings,
    bool RunElevated = false);
