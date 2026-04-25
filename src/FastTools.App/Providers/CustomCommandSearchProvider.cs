using FastTools.App.Infrastructure;
using FastTools.App.Models;
using FastTools.App.Services;
using System.Diagnostics;

namespace FastTools.App.Providers;

public sealed class CustomCommandSearchProvider : ISearchProvider
{
    private readonly LauncherSettingsStore _settingsStore;

    public CustomCommandSearchProvider(LauncherSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var results = _settingsStore.Current.CustomCommands
            .Select(command => new
            {
                Command = command,
                Score = string.IsNullOrWhiteSpace(query)
                    ? 10
                    : SearchMatcher.Score(query, command.Name, command.Alias, command.Command),
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .Take(8)
            .Select(match => new SearchResultItem
            {
                Key = $"command:{match.Command.Id}",
                Title = match.Command.Name,
                Subtitle = $"{match.Command.Command} {match.Command.Arguments}".Trim(),
                Group = "Commands",
                Glyph = "\uE756",
                Score = match.Score,
                RequiresConfirmation = match.Command.RequiresConfirmation,
                ConfirmationMessage = match.Command.ConfirmationMessage,
                ExecuteAsync = _ =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = match.Command.Command,
                        Arguments = match.Command.Arguments,
                        UseShellExecute = true,
                    });

                    return Task.CompletedTask;
                },
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResultItem>>(results);
    }
}
