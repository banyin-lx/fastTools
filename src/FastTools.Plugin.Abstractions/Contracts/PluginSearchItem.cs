namespace FastTools.Plugin.Abstractions.Contracts;

public sealed record PluginSearchItem(
    string Key,
    string Title,
    string Subtitle,
    string Category,
    string Glyph,
    double Score = 0,
    bool RequiresConfirmation = false,
    string? ConfirmationMessage = null,
    string? IconPath = null);
