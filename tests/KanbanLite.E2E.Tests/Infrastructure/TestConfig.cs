using System.Text.Json;

namespace KanbanLite.E2E.Tests.Infrastructure;

/// <summary>
/// Konfiguracja testów E2E - adresy, dane testowe, timeouty.
/// </summary>
public static class TestConfig
{
    private static readonly TestSettings _settings;

    static TestConfig()
    {
        string configFileName;
#if DEBUG
        configFileName = "appsettings.e2e.Debug.json";
#else
        configFileName = "appsettings.e2e.Release.json";
#endif
        var path = Path.Combine(AppContext.BaseDirectory, configFileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found at {path}");
        }

        var json = File.ReadAllText(path);
        _settings = JsonSerializer.Deserialize<TestSettings>(json)
                    ?? throw new InvalidOperationException("Failed to deserialize configuration.");

        // Obsługa nadpisywania przez zmienne środowiskowe (dla CI/CD w GitHub Actions)
        var envBaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBaseUrl)) _settings.BaseUrl = envBaseUrl;

        var envManagerUser = Environment.GetEnvironmentVariable("E2E_MANAGER_USERNAME");
        if (!string.IsNullOrWhiteSpace(envManagerUser)) _settings.ManagerUser!.Username = envManagerUser;

        var envManagerPass = Environment.GetEnvironmentVariable("E2E_MANAGER_PASSWORD");
        if (!string.IsNullOrWhiteSpace(envManagerPass)) _settings.ManagerUser!.Password = envManagerPass;

        var envOperatorUser = Environment.GetEnvironmentVariable("E2E_OPERATOR_USERNAME");
        if (!string.IsNullOrWhiteSpace(envOperatorUser)) _settings.OperatorUser!.Username = envOperatorUser;

        var envOperatorPass = Environment.GetEnvironmentVariable("E2E_OPERATOR_PASSWORD");
        if (!string.IsNullOrWhiteSpace(envOperatorPass)) _settings.OperatorUser!.Password = envOperatorPass;

        // Walidacja
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
            throw new InvalidOperationException("BaseUrl is missing in config.");

        if (_settings.ManagerUser == null || string.IsNullOrWhiteSpace(_settings.ManagerUser.Username) || string.IsNullOrWhiteSpace(_settings.ManagerUser.Password))
            throw new InvalidOperationException("ManagerUser configuration is incomplete.");

        if (_settings.OperatorUser == null || string.IsNullOrWhiteSpace(_settings.OperatorUser.Username) || string.IsNullOrWhiteSpace(_settings.OperatorUser.Password))
            throw new InvalidOperationException("OperatorUser configuration is incomplete.");

        if (_settings.Supabase == null || string.IsNullOrWhiteSpace(_settings.Supabase.Url) || string.IsNullOrWhiteSpace(_settings.Supabase.Key))
            throw new InvalidOperationException("Supabase configuration is incomplete.");
    }

    /// <summary>
    /// Bazowy URL aplikacji do testowania.
    /// </summary>
    public static string BaseUrl => _settings.BaseUrl!;

    public static bool Headless => _settings.Headless;
    public static int SlowMo => _settings.SlowMo;

    /// <summary>
    /// Konfiguracja Supabase
    /// </summary>
    public static class Supabase
    {
        public static string Url => _settings.Supabase!.Url!;
        public static string Key => _settings.Supabase!.Key!;
    }

    /// <summary>
    /// Dane logowania użytkownika Manager.
    /// </summary>
    public static class ManagerUser
    {
        public static string Username => _settings.ManagerUser!.Username!;
        public static string Password => _settings.ManagerUser!.Password!;
    }

    /// <summary>
    /// Dane logowania użytkownika Operator.
    /// </summary>
    public static class OperatorUser
    {
        public static string Username => _settings.OperatorUser!.Username!;
        public static string Password => _settings.OperatorUser!.Password!;
    }

    /// <summary>
    /// Timeouty dla operacji testowych.
    /// </summary>
    public static class Timeouts
    {
        public static float DefaultTimeout => 30_000;
        public static float NavigationTimeout => 120_000; // 2 minuty
        public static float ActionTimeout => 10_000;
    }

    public class TestSettings
    {
        public string? BaseUrl { get; set; }
        public bool Headless { get; set; }
        public int SlowMo { get; set; }
        public UserCredentials? ManagerUser { get; set; }
        public UserCredentials? OperatorUser { get; set; }
        public SupabaseSettings? Supabase { get; set; }
    }

    public class UserCredentials
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class SupabaseSettings
    {
        public string? Url { get; set; }
        public string? Key { get; set; }
    }
}
