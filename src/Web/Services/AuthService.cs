using System.Security.Claims;
using DataAccess.Identity;
using KanbanLite.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;

namespace KanbanLite.Web.Services;

public sealed class AuthService : IAuthService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AuthService> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Result<LoginResult>> LoginAsync(string username, string password, bool isPersistent = false)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Result<LoginResult>.Fail(
                AppError.ValidationFailed("Nazwa użytkownika jest wymagana.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["username"] = ["Nazwa użytkownika nie może być pusta."]
                }));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Result<LoginResult>.Fail(
                AppError.ValidationFailed("Hasło jest wymagane.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["password"] = ["Hasło nie może być puste."]
                }));
        }

        try
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user is null)
            {
                _logger.LogWarning("Próba logowania nieistniejącym użytkownikiem: {Username}", username);
                return Result<LoginResult>.Fail(
                    AppError.ValidationFailed("Nieprawidłowa nazwa użytkownika lub hasło."));
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
            
            if (!result.Succeeded)
            {
                _logger.LogWarning("Nieudana próba logowania dla użytkownika: {Username}", username);
                return Result<LoginResult>.Fail(
                    AppError.ValidationFailed("Nieprawidłowa nazwa użytkownika lub hasło."));
            }

            // Pobranie ról użytkownika
            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogInformation("Weryfikacja logowania użytkownika {Username} zakończona pomyślnie", username);

            // Zwracamy użytkownika - SignInAsync zostanie wywołane w Login.razor.cs po zakończeniu połączenia SignalR
            return Result<LoginResult>.Ok(new LoginResult(
                user.Id,
                user.UserName ?? username,
                roles.ToList().AsReadOnly(),
                user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas logowania użytkownika: {Username}", username);
            return Result<LoginResult>.Fail(
                AppError.Unexpected("Wystąpił błąd podczas logowania. Spróbuj ponownie."));
        }
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Użytkownik wylogował się");
    }
}
