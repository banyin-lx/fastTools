using FastTools.Plugin.Abstractions.Contracts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;

namespace FastTools.App.Views;

public abstract class PluginSettingItem
{
    protected PluginSettingItem(string key, string label, string? description)
    {
        Key = key;
        Label = label;
        Description = description ?? string.Empty;
    }

    public string Key { get; }

    public string Label { get; }

    public string Description { get; }

    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public abstract string SerializeValue();

    public static PluginSettingItem Create(
        PluginSettingDefinition definition,
        IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue(definition.Key, out var storedValue);
        return definition switch
        {
            PluginSelectSettingDefinition select => PluginSelectSettingItem.From(select, storedValue),
            PluginDirectoryListSettingDefinition directories => PluginDirectoryListSettingItem.From(directories, storedValue),
            _ => new PluginUnknownSettingItem(definition.Key, definition.Label, definition.Description),
        };
    }
}

public sealed class PluginSelectSettingItem : PluginSettingItem, INotifyPropertyChanged
{
    private string _selectedOption = string.Empty;

    private PluginSelectSettingItem(
        string key,
        string label,
        string? description,
        IReadOnlyList<string> options,
        string selectedOption)
        : base(key, label, description)
    {
        Options = new ObservableCollection<string>(options);
        _selectedOption = selectedOption;
    }

    public ObservableCollection<string> Options { get; }

    public string SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (_selectedOption == value)
            {
                return;
            }

            _selectedOption = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string SerializeValue()
    {
        return SelectedOption;
    }

    public static PluginSelectSettingItem From(PluginSelectSettingDefinition definition, string? storedValue)
    {
        var selected = definition.Options.Contains(storedValue, StringComparer.OrdinalIgnoreCase)
            ? storedValue!
            : definition.DefaultValue;

        return new PluginSelectSettingItem(
            definition.Key,
            definition.Label,
            definition.Description,
            definition.Options,
            selected);
    }
}

public sealed class PluginDirectoryListSettingItem : PluginSettingItem
{
    private PluginDirectoryListSettingItem(
        string key,
        string label,
        string? description,
        IReadOnlyList<string> directories)
        : base(key, label, description)
    {
        Directories = new ObservableCollection<string>(directories);
    }

    public ObservableCollection<string> Directories { get; }

    public override string SerializeValue()
    {
        return JsonSerializer.Serialize(Directories);
    }

    public void AddDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Directories.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        Directories.Add(path);
    }

    public void RemoveDirectory(string path)
    {
        var existing = Directories.FirstOrDefault(item => item.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        Directories.Remove(existing);
    }

    public static PluginDirectoryListSettingItem From(PluginDirectoryListSettingDefinition definition, string? storedValue)
    {
        var directories = new List<string>();
        if (!string.IsNullOrWhiteSpace(storedValue))
        {
            try
            {
                directories = JsonSerializer.Deserialize<List<string>>(storedValue) ?? [];
            }
            catch
            {
            }
        }

        directories = directories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PluginDirectoryListSettingItem(
            definition.Key,
            definition.Label,
            definition.Description,
            directories);
    }
}

public sealed class PluginUnknownSettingItem : PluginSettingItem
{
    public PluginUnknownSettingItem(string key, string label, string? description)
        : base(key, label, description)
    {
    }

    public override string SerializeValue()
    {
        return string.Empty;
    }
}
