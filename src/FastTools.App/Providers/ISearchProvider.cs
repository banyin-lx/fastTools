using FastTools.App.Models;

namespace FastTools.App.Providers;

public interface ISearchProvider
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken cancellationToken);
}
