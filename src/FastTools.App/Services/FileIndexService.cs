using FastTools.App.Infrastructure;
using FastTools.App.Models;

namespace FastTools.App.Services;

public sealed class FileIndexService
{
    private readonly LauncherSettingsStore _settingsStore;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<FileIndexEntry> _cache = [];
    private Task? _backgroundRefreshTask;
    private bool _loaded;

    public FileIndexService(LauncherSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settingsStore.SettingsChanged += (_, _) => _loaded = false;
    }

    public bool IsLoaded => _loaded;

    public IReadOnlyList<FileIndexEntry> GetSnapshot()
    {
        return _cache;
    }

    public Task EnsureBackgroundRefreshAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        if (_backgroundRefreshTask is { IsCompleted: false })
        {
            return _backgroundRefreshTask;
        }

        _backgroundRefreshTask = RefreshAsync();
        return _backgroundRefreshTask;
    }

    public async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var results = await Task.Run(() =>
            {
                var batch = new List<FileIndexEntry>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var root in _settingsStore.Current.IndexedDirectories.Where(Directory.Exists))
                {
                    EnumerateEntries(root, batch, seen);
                }

                return (IReadOnlyList<FileIndexEntry>)batch;
            }).ConfigureAwait(false);

            _cache = results;
            _loaded = true;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static void EnumerateEntries(string root, List<FileIndexEntry> results, HashSet<string> seen)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (!seen.Add(directory))
                {
                    continue;
                }

                results.Add(new FileIndexEntry
                {
                    FullPath = directory,
                    Name = Path.GetFileName(directory),
                    IsDirectory = true,
                    ParentDirectory = Directory.GetParent(directory)?.FullName ?? root,
                    NormalizedName = SearchMatcher.Normalize(Path.GetFileName(directory)),
                    NormalizedParentDirectory = SearchMatcher.Normalize(Directory.GetParent(directory)?.FullName ?? root),
                });
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!seen.Add(file))
                {
                    continue;
                }

                results.Add(new FileIndexEntry
                {
                    FullPath = file,
                    Name = Path.GetFileName(file),
                    IsDirectory = false,
                    ParentDirectory = Path.GetDirectoryName(file) ?? root,
                    NormalizedName = SearchMatcher.Normalize(Path.GetFileName(file)),
                    NormalizedParentDirectory = SearchMatcher.Normalize(Path.GetDirectoryName(file) ?? root),
                });
            }
        }
        catch
        {
        }
    }
}
