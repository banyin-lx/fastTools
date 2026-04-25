namespace FastTools.Plugin.Abstractions.Contracts;

public sealed record PluginQuery(
    string Text,
    IReadOnlyDictionary<string, string> Settings);
