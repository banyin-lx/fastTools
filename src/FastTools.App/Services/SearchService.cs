using FastTools.App.Models;
using FastTools.App.Providers;

namespace FastTools.App.Services;

public sealed class SearchService
{
    private readonly IReadOnlyList<ISearchProvider> _providers;
    private readonly LocalizationService _localizer;
    private readonly LauncherSettingsStore _settingsStore;

    public SearchService(
        IEnumerable<ISearchProvider> providers,
        LauncherSettingsStore settingsStore,
        LocalizationService localizer)
    {
        _providers = providers.ToList();
        _settingsStore = settingsStore;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var tasks = _providers.Select(provider => SearchProviderAsync(provider, query, cancellationToken));
        var batches = await Task.WhenAll(tasks).ConfigureAwait(false);

        var normalizedQuery = query.Trim();
        var results = batches
            .SelectMany(batch => batch)
            .Where(item => _settingsStore.Current.IsGroupEnabled(item.Group))
            .GroupBy(result => result.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .Select(BoostByUsage)
            .Select(ApplyLocalizedGroup)
            .OrderBy(item => _settingsStore.Current.GetPriorityForGroup(item.Group))
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(string.IsNullOrWhiteSpace(normalizedQuery) ? 12 : 32)
            .ToList();

        return results;
    }

    public Task RecordUsageAsync(SearchResultItem item)
    {
        return _settingsStore.IncrementUsageAsync(item.Key);
    }

    private SearchResultItem BoostByUsage(SearchResultItem item)
    {
        if (_settingsStore.Current.UsageCounts.TryGetValue(item.Key, out var usage))
        {
            item.Score += Math.Min(usage * 6, 60);
        }

        return item;
    }

    private SearchResultItem ApplyLocalizedGroup(SearchResultItem item)
    {
        item.DisplayGroup = _localizer.Get($"Group.{item.Group}");
        return item;
    }

    private static Task<IReadOnlyList<SearchResultItem>> SearchProviderAsync(
        ISearchProvider provider,
        string query,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }
}
