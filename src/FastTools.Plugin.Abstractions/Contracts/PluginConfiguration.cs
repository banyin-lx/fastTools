namespace FastTools.Plugin.Abstractions.Contracts;

public sealed record PluginConfiguration(
    IReadOnlyList<PluginSettingDefinition> Settings)
{
    public static PluginConfiguration Empty { get; } = new([]);
}

public abstract record PluginSettingDefinition(
    string Key,
    string Label,
    string? Description = null);

public sealed record PluginSelectSettingDefinition(
    string Key,
    string Label,
    IReadOnlyList<string> Options,
    string DefaultValue,
    string? Description = null) : PluginSettingDefinition(Key, Label, Description);

public sealed record PluginDirectoryListSettingDefinition(
    string Key,
    string Label,
    string? Description = null) : PluginSettingDefinition(Key, Label, Description);
