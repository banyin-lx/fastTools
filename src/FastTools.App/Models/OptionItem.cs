namespace FastTools.App.Models;

public sealed class OptionItem<T>
{
    public required T Value { get; init; }

    public required string Label { get; init; }
}
