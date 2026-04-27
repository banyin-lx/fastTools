using FastTools.App.Infrastructure;
using FastTools.App.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastTools.App.Services;

public sealed class LocalizationService : ObservableObject
{
    public const string DefaultLanguageCode = "zh-CN";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, LanguagePackage> _packages = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = DefaultLanguageCode;

    public LocalizationService()
    {
        LoadPackages();

        if (_packages.Count == 0)
        {
            // Guarantee at least one usable package so the app does not crash.
            var fallback = new LanguagePackage
            {
                Code = DefaultLanguageCode,
                DisplayName = DefaultLanguageCode,
                Strings = new Dictionary<string, string>(StringComparer.Ordinal),
            };
            _packages[fallback.Code] = fallback;
        }

        if (!_packages.ContainsKey(_currentLanguage))
        {
            _currentLanguage = _packages.Keys.First();
        }

        AvailableLanguages = _packages.Values
            .Select(package => new LanguageDescriptor
            {
                Code = package.Code,
                DisplayName = package.DisplayName,
            })
            .OrderBy(descriptor => descriptor.DisplayName, StringComparer.CurrentCulture)
            .ToList();
    }

    public IReadOnlyList<LanguageDescriptor> AvailableLanguages { get; }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set => SetProperty(ref _currentLanguage, value);
    }

    public string this[string key] => Get(key);

    public void Apply(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            languageCode = DefaultLanguageCode;
        }

        if (!_packages.ContainsKey(languageCode))
        {
            languageCode = _packages.ContainsKey(DefaultLanguageCode)
                ? DefaultLanguageCode
                : _packages.Keys.First();
        }

        CurrentLanguage = languageCode;
        OnPropertyChanged("Item[]");
    }

    public string Get(string key)
    {
        if (_packages.TryGetValue(_currentLanguage, out var package) &&
            package.Strings.TryGetValue(key, out var value))
        {
            return value;
        }

        if (!_currentLanguage.Equals(DefaultLanguageCode, StringComparison.OrdinalIgnoreCase) &&
            _packages.TryGetValue(DefaultLanguageCode, out var fallback) &&
            fallback.Strings.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    private void LoadPackages()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Languages");
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var package = JsonSerializer.Deserialize<LanguagePackage>(stream, SerializerOptions);
                if (package is null || string.IsNullOrWhiteSpace(package.Code))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(package.DisplayName))
                {
                    package.DisplayName = package.Code;
                }

                package.Strings ??= new Dictionary<string, string>(StringComparer.Ordinal);
                _packages[package.Code] = package;
            }
            catch
            {
                // Skip malformed language files silently; users can correct them and reload.
            }
        }
    }

    private sealed class LanguagePackage
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("strings")]
        public Dictionary<string, string> Strings { get; set; } = new(StringComparer.Ordinal);
    }
}
