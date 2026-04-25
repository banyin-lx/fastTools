using FastTools.Plugin.Abstractions.Contracts;
using System.Diagnostics;

namespace FastTools.Plugin.SystemCommands.Plugins;

public sealed class SystemCommandsPlugin : ILauncherPlugin
{
    private static readonly IReadOnlyList<SystemCommand> Commands =
    [
        new("lock", "Lock Screen", "立即锁定当前设备", "system lock", "\uE72E", "rundll32.exe", "user32.dll,LockWorkStation"),
        new("taskmgr", "Task Manager", "打开任务管理器", "task manager process monitor", "\uE9D9", "taskmgr.exe", string.Empty),
        new("control-panel", "Control Panel", "打开控制面板", "control panel settings", "\uE713", "control.exe", string.Empty),
        new("windows-settings", "Windows Settings", "打开 Windows 设置", "settings preferences", "\uE713", "ms-settings:", string.Empty),
        new("sleep", "Sleep", "让电脑进入睡眠", "sleep suspend", "\uE823", "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", true, "确定让电脑进入睡眠？"),
        new("restart", "Restart", "60 秒后重启电脑", "restart reboot", "\uE7E8", "shutdown.exe", "/r /t 60", true, "确定执行重启？"),
        new("shutdown", "Shut Down", "60 秒后关闭电脑", "shutdown poweroff", "\uE7E8", "shutdown.exe", "/s /t 60", true, "确定执行关机？"),
        new("empty-recycle-bin", "Empty Recycle Bin", "清空回收站", "recycle bin trash", "\uE74D", "powershell.exe", "-NoProfile -Command Clear-RecycleBin -Force", true, "确定清空回收站？"),
    ];

    public string Id => "fasttools.system";

    public string DisplayName => "System Commands";

    public string Description => "提供锁屏、任务管理器、控制面板、关机等系统动作。";

    public Task<IReadOnlyList<PluginSearchItem>> QueryAsync(PluginQuery query, CancellationToken cancellationToken)
    {
        var results = Commands
            .Select(command => new
            {
                Command = command,
                Score = Score(query.Text, command.Title, command.Keywords),
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .Take(string.IsNullOrWhiteSpace(query.Text) ? 4 : 8)
            .Select(match => new PluginSearchItem(
                match.Command.Id,
                match.Command.Title,
                match.Command.Description,
                "Commands",
                match.Command.Glyph,
                match.Score,
                match.Command.RequiresConfirmation,
                match.Command.ConfirmationMessage))
            .ToList();

        return Task.FromResult<IReadOnlyList<PluginSearchItem>>(results);
    }

    public Task ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken)
    {
        var command = Commands.First(item => item.Id == request.Item.Key);
        Process.Start(new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = command.Arguments,
            UseShellExecute = true,
        });

        return Task.CompletedTask;
    }

    private static double Score(string query, string title, string keywords)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 10;
        }

        var queryValue = Normalize(query);
        var titleValue = Normalize(title);
        var keywordValue = Normalize(keywords);

        if (titleValue.StartsWith(queryValue, StringComparison.Ordinal))
        {
            return 160;
        }

        if (titleValue.Contains(queryValue, StringComparison.Ordinal) ||
            keywordValue.Contains(queryValue, StringComparison.Ordinal))
        {
            return 120;
        }

        return 0;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private sealed record SystemCommand(
        string Id,
        string Title,
        string Description,
        string Keywords,
        string Glyph,
        string FileName,
        string Arguments,
        bool RequiresConfirmation = false,
        string? ConfirmationMessage = null);
}
