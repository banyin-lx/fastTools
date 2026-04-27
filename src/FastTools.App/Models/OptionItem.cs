using System.ComponentModel;

namespace FastTools.App.Models;

public sealed class OptionItem<T> : INotifyPropertyChanged
{
    private string _label = string.Empty;

    public required T Value { get; init; }

    public required string Label
    {
        get => _label;
        set
        {
            if (_label == value)
            {
                return;
            }

            _label = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
