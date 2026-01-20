using System.Net.Http;

namespace KanbanLite.Web.Services;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        HttpClient httpClient,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        try
        {
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });

            var response = await _httpClient.PostAsync("/api/auth/login", formData);

            if (response.IsSuccessStatusCode)
            {
                var redirectUrl = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Logowanie użytkownika {Username} zakończone pomyślnie", username);
                return redirectUrl;
            }

            _logger.LogWarning("Nieudana próba logowania dla użytkownika: {Username}", username);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas logowania użytkownika: {Username}", username);
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/auth/logout", null);
            _logger.LogInformation("Użytkownik wylogował się");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wylogowania użytkownika");
        }
    }
}
