using FastTools.App.Models;
using System.Text.Json;

namespace FastTools.App.Services;

public sealed class LauncherSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _settingsPath;

    public LauncherSettingsStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FastTools");
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
    }

    public LauncherSettings Current { get; private set; } = LauncherSettings.CreateDefault();

    public event EventHandler<LauncherSettings>? SettingsChanged;

    public async Task<LauncherSettings> LoadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                Current = LauncherSettings.CreateDefault();
                await SaveCoreAsync(Current).ConfigureAwait(false);
                return Current.Clone();
            }

            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, SerializerOptions).ConfigureAwait(false)
                ?? LauncherSettings.CreateDefault();
            settings.EnsureDefaults();
            Current = settings;
            return Current.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(LauncherSettings settings)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Current = settings.Clone();
            await SaveCoreAsync(Current).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        SettingsChanged?.Invoke(this, Current.Clone());
    }

    public async Task IncrementUsageAsync(string key)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Current.UsageCounts.TryGetValue(key, out var count);
            Current.UsageCounts[key] = count + 1;
            await SaveCoreAsync(Current).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveCoreAsync(LauncherSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions).ConfigureAwait(false);
    }
}
