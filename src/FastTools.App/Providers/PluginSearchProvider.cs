using FastTools.App.Models;
using FastTools.App.Services;

namespace FastTools.App.Providers;

public sealed class PluginSearchProvider : ISearchProvider
{
    private readonly PluginHostService _pluginHost;

    public PluginSearchProvider(PluginHostService pluginHost)
    {
        _pluginHost = pluginHost;
    }

    public Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        return _pluginHost.SearchAsync(query, cancellationToken);
    }
}
