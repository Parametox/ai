using DataAccess.Identity;
using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KanbanLite.Web.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ISessionService sessionService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _sessionService = sessionService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromQuery] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Próba logowania z pustymi danymi");
            return Redirect("/login?error=true");
        }

        // Znajdź użytkownika
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
        {
            _logger.LogWarning("Nieudana próba logowania - użytkownik nie istnieje: {Username}", username);
            return Redirect("/login?error=true");
        }

        // Sprawdź hasło
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Nieudana próba logowania - nieprawidłowe hasło dla użytkownika: {Username}", username);
            return Redirect("/login?error=true");
        }

        // Pobierz role użytkownika
        var roles = await _userManager.GetRolesAsync(user);

        // Ustaw sesję w SessionService
        _sessionService.SetSession(user.Id, user.UserName ?? username, roles);

        _logger.LogInformation("Użytkownik {Username} zalogował się pomyślnie", username);
        
        // Przekieruj na ReturnUrl lub domyślnie na /kanban
        var redirectUrl = !string.IsNullOrEmpty(returnUrl) && returnUrl != "/"
            ? returnUrl
            : "/kanban";
        
        return Redirect(redirectUrl);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _sessionService.ClearSession();
        _logger.LogInformation("Użytkownik wylogował się");
        return Redirect("/login");
    }
}
