using FastTools.App.Models;
using System.Collections.ObjectModel;

namespace FastTools.App.Services;

public sealed class LogService
{
    private const int MaxEntries = 500;

    public static LogService Instance { get; } = new();

    private readonly object _gate = new();

    private LogService()
    {
        Entries = new ObservableCollection<LogEntry>();
    }

    public ObservableCollection<LogEntry> Entries { get; }

    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    public bool IsEnabled { get; set; } = true;

    public LocalizationService? Localizer { get; set; }

    public void LogKey(LogLevel level, string source, string key, params object[] args)
    {
        var template = Localizer?.Get(key) ?? key;
        string message;
        try
        {
            message = args is { Length: > 0 } ? string.Format(template, args) : template;
        }
        catch (FormatException)
        {
            message = template;
        }
        Log(level, source, message);
    }

    public void TraceKey(string source, string key, params object[] args) => LogKey(LogLevel.Trace, source, key, args);
    public void DebugKey(string source, string key, params object[] args) => LogKey(LogLevel.Debug, source, key, args);
    public void InfoKey(string source, string key, params object[] args) => LogKey(LogLevel.Info, source, key, args);
    public void WarnKey(string source, string key, params object[] args) => LogKey(LogLevel.Warn, source, key, args);
    public void ErrorKey(string source, string key, params object[] args) => LogKey(LogLevel.Error, source, key, args);

    public void Log(LogLevel level, string source, string message)
    {
        if (!IsEnabled || level < MinLevel)
        {
            return;
        }

        var entry = new LogEntry(DateTime.Now, level, source, message);

        // Mirror to Debug output for Visual Studio.
        System.Diagnostics.Debug.WriteLine($"[{entry.FormattedTimestamp}] [{entry.LevelText}] {source}: {message}");

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AppendEntry(entry);
        }
        else
        {
            dispatcher.BeginInvoke(() => AppendEntry(entry));
        }
    }

    public void Trace(string source, string message) => Log(LogLevel.Trace, source, message);

    public void Debug(string source, string message) => Log(LogLevel.Debug, source, message);

    public void Info(string source, string message) => Log(LogLevel.Info, source, message);

    public void Warn(string source, string message) => Log(LogLevel.Warn, source, message);

    public void Error(string source, string message) => Log(LogLevel.Error, source, message);

    public void Clear()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            lock (_gate)
            {
                Entries.Clear();
            }
        }
        else
        {
            dispatcher.BeginInvoke(Clear);
        }
    }

    private void AppendEntry(LogEntry entry)
    {
        lock (_gate)
        {
            Entries.Add(entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        }
    }
}
