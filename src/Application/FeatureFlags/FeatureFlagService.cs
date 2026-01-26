namespace KanbanLite.Application.FeatureFlags;

/// <summary>
/// Nazwy dostępnych feature flag w systemie.
/// </summary>
public static class FeatureNames
{
    /// <summary>
    /// Funkcja Dashboard - panel Managera z metrykami.
    /// </summary>
    public const string Dashboard = "Dashboard";

    /// <summary>
    /// Funkcja Audit - historia zmian batchy.
    /// </summary>
    public const string Audit = "Audit";

    /// <summary>
    /// Funkcja Konfiguracja - zarządzanie regułami splitu i formatami.
    /// </summary>
    public const string Configuration = "Configuration";

    /// <summary>
    /// Funkcja Kanban - główna tablica Kanban.
    /// </summary>
    public const string Kanban = "Kanban";
}

/// <summary>
/// Nazwy środowisk aplikacji.
/// </summary>
public static class EnvironmentNames
{
    public const string Local = "local";
    public const string Development = "development";
    public const string Staging = "staging";
    public const string Production = "production";
}

/// <summary>
/// Interfejs serwisu Feature Flags.
/// Pozwala na sprawdzanie dostępności funkcji w różnych środowiskach.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Sprawdza czy dana funkcja jest włączona.
    /// </summary>
    /// <param name="featureName">Nazwa funkcji (użyj stałych z <see cref="FeatureNames"/>).</param>
    /// <returns>True jeśli funkcja jest włączona, false w przeciwnym razie.</returns>
    bool IsEnabled(string featureName);

    /// <summary>
    /// Pobiera aktualne środowisko.
    /// </summary>
    string CurrentEnvironment { get; }

    /// <summary>
    /// Pobiera wszystkie flagi dla aktualnego środowiska.
    /// </summary>
    IReadOnlyDictionary<string, bool> GetAllFlags();
}

/// <summary>
/// Konfiguracja Feature Flags dla różnych środowisk.
/// </summary>
public class FeatureFlagConfiguration
{
    /// <summary>
    /// Flagi dla środowiska lokalnego (development).
    /// </summary>
    public Dictionary<string, bool> Local { get; set; } = new();

    /// <summary>
    /// Flagi dla środowiska development (CI/testy).
    /// </summary>
    public Dictionary<string, bool> Development { get; set; } = new();

    /// <summary>
    /// Flagi dla środowiska staging.
    /// </summary>
    public Dictionary<string, bool> Staging { get; set; } = new();

    /// <summary>
    /// Flagi dla środowiska produkcyjnego.
    /// </summary>
    public Dictionary<string, bool> Production { get; set; } = new();
}

/// <summary>
/// Implementacja serwisu Feature Flags.
/// Obsługuje konfigurację z appsettings.json oraz zmiennej środowiskowej APP_ENVIRONMENT.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly Dictionary<string, Dictionary<string, bool>> _featureConfig;
    private readonly string _currentEnvironment;

    /// <summary>
    /// Domyślna konfiguracja flag dla wszystkich środowisk.
    /// W środowisku lokalnym wszystko włączone, na produkcji - ostrożniej.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, bool>> DefaultConfig = new()
    {
        [EnvironmentNames.Local] = new()
        {
            [FeatureNames.Dashboard] = true,
            [FeatureNames.Audit] = true,
            [FeatureNames.Configuration] = true,
            [FeatureNames.Kanban] = true,
        },
        [EnvironmentNames.Development] = new()
        {
            [FeatureNames.Dashboard] = true,
            [FeatureNames.Audit] = true,
            [FeatureNames.Configuration] = true,
            [FeatureNames.Kanban] = true,
        },
        [EnvironmentNames.Staging] = new()
        {
            [FeatureNames.Dashboard] = true,
            [FeatureNames.Audit] = true,
            [FeatureNames.Configuration] = true,
            [FeatureNames.Kanban] = true,
        },
        [EnvironmentNames.Production] = new()
        {
            [FeatureNames.Dashboard] = true,
            [FeatureNames.Audit] = true,
            [FeatureNames.Configuration] = true,
            [FeatureNames.Kanban] = true,
        }
    };

    /// <summary>
    /// Tworzy nową instancję FeatureFlagService.
    /// </summary>
    /// <param name="configuration">Opcjonalna konfiguracja z appsettings. Jeśli null, używa domyślnej.</param>
    /// <param name="environmentOverride">Opcjonalne nadpisanie środowiska (głównie do testów).</param>
    public FeatureFlagService(FeatureFlagConfiguration? configuration = null, string? environmentOverride = null)
    {
        _currentEnvironment = ResolveEnvironment(environmentOverride);
        _featureConfig = BuildFeatureConfig(configuration);
    }

    /// <inheritdoc />
    public string CurrentEnvironment => _currentEnvironment;

    /// <inheritdoc />
    public bool IsEnabled(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            return false;

        // Pobierz konfigurację dla aktualnego środowiska
        if (!_featureConfig.TryGetValue(_currentEnvironment, out var envFlags))
        {
            // Nieznane środowisko - domyślnie wszystko wyłączone (bezpieczeństwo)
            return false;
        }

        // Sprawdź flagę
        if (envFlags.TryGetValue(featureName, out var isEnabled))
        {
            return isEnabled;
        }

        // Nieznana flaga - domyślnie wyłączona
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, bool> GetAllFlags()
    {
        if (_featureConfig.TryGetValue(_currentEnvironment, out var envFlags))
        {
            return envFlags;
        }

        return new Dictionary<string, bool>();
    }

    /// <summary>
    /// Określa aktualne środowisko na podstawie zmiennych środowiskowych.
    /// Kolejność sprawdzania: APP_ENVIRONMENT → ASPNETCORE_ENVIRONMENT → DOTNET_ENVIRONMENT → "production".
    /// </summary>
    private static string ResolveEnvironment(string? environmentOverride)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return NormalizeEnvironment(environmentOverride);
        }

        var envName = Environment.GetEnvironmentVariable("APP_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        // Jeśli brak zmiennej środowiskowej - domyślnie production (bezpieczeństwo)
        if (string.IsNullOrWhiteSpace(envName))
        {
            return EnvironmentNames.Production;
        }

        return NormalizeEnvironment(envName);
    }

    /// <summary>
    /// Normalizuje nazwę środowiska do jednej z obsługiwanych wartości.
    /// </summary>
    private static string NormalizeEnvironment(string envName)
    {
        return envName.ToLowerInvariant() switch
        {
            "local" or "localhost" => EnvironmentNames.Local,
            "development" or "dev" => EnvironmentNames.Development,
            "staging" or "stage" or "test" or "integration" => EnvironmentNames.Staging,
            "production" or "prod" or "release" => EnvironmentNames.Production,
            _ => EnvironmentNames.Production // Nieznane = bezpieczna produkcja
        };
    }

    /// <summary>
    /// Buduje finalną konfigurację łącząc domyślne wartości z przekazaną konfiguracją.
    /// </summary>
    private static Dictionary<string, Dictionary<string, bool>> BuildFeatureConfig(FeatureFlagConfiguration? config)
    {
        var result = new Dictionary<string, Dictionary<string, bool>>();

        // Kopiuj domyślne wartości
        foreach (var (env, flags) in DefaultConfig)
        {
            result[env] = new Dictionary<string, bool>(flags);
        }

        // Nadpisz wartościami z konfiguracji (jeśli dostarczone)
        if (config != null)
        {
            MergeFlags(result, EnvironmentNames.Local, config.Local);
            MergeFlags(result, EnvironmentNames.Development, config.Development);
            MergeFlags(result, EnvironmentNames.Staging, config.Staging);
            MergeFlags(result, EnvironmentNames.Production, config.Production);
        }

        return result;
    }

    /// <summary>
    /// Łączy flagi z konfiguracji do istniejącego słownika.
    /// </summary>
    private static void MergeFlags(Dictionary<string, Dictionary<string, bool>> result, string env, Dictionary<string, bool>? flags)
    {
        if (flags == null || flags.Count == 0)
            return;

        if (!result.ContainsKey(env))
        {
            result[env] = new Dictionary<string, bool>();
        }

        foreach (var (key, value) in flags)
        {
            result[env][key] = value;
        }
    }
}
