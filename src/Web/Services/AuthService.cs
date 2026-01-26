using DataAccess.Identity;
using Microsoft.AspNetCore.Identity;

namespace KanbanLite.Web.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ISessionService sessionService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Próba logowania z pustymi danymi");
                return null;
            }

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                _logger.LogWarning("Nieudana próba logowania - użytkownik nie istnieje: {Username}", username);
                return null;
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
            if (!isPasswordValid)
            {
                _logger.LogWarning("Nieudana próba logowania - nieprawidłowe hasło dla użytkownika: {Username}", username);
                return null;
            }

            // Pobierz role użytkownika
            var roles = await _userManager.GetRolesAsync(user);

            // Ustawienie sesji w SessionService
            _sessionService.SetSession(user.Id, user.UserName ?? username, roles);
            _logger.LogInformation("Użytkownik {Username} zalogował się pomyślnie", username);

            return "/kanban";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas logowania użytkownika: {Username}", username);
            return null;
        }
    }

    public Task LogoutAsync()
    {
        try
        {
            _sessionService.ClearSession();
            _logger.LogInformation("Użytkownik wylogował się");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wylogowania użytkownika");
        }

        return Task.CompletedTask;
    }
}
