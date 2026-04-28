namespace FastTools.App.Models;

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
}

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Source, string Message)
{
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public string LevelText => Level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warn => "WRN",
        LogLevel.Error => "ERR",
        _ => "LOG",
    };

    public string LevelColor => Level switch
    {
        LogLevel.Trace => "#5A6B7A",
        LogLevel.Debug => "#3E627D",
        LogLevel.Info  => "#3A8DAA",
        LogLevel.Warn  => "#C18A3B",
        LogLevel.Error => "#B0413E",
        _ => "#3E627D",
    };

    /// <summary>
    /// Renders the entry as a single line with inline color markup, e.g.
    /// "15:30:42.123 [#3A8DAA]INF[/] [Search] message".
    /// Markup tokens: [#RRGGBB]...[/]
    /// </summary>
    public string Markup =>
        $"{FormattedTimestamp} [{LevelColor}]{LevelText}[/] [{Source}] {Message}";
}
