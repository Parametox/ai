using DataAccess.Identity;
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
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _signInManager = signInManager;
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

        var result = await _signInManager.PasswordSignInAsync(
            username,
            password,
            isPersistent: true,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("Użytkownik {Username} zalogował się pomyślnie", username);
            
            // Przekieruj na ReturnUrl lub domyślnie na /kanban
            var redirectUrl = !string.IsNullOrEmpty(returnUrl) && returnUrl != "/"
                ? returnUrl
                : "/kanban";
            
            return Ok(redirectUrl);
        }

        _logger.LogWarning("Nieudana próba logowania dla użytkownika: {Username}", username);
        return Redirect("/login?error=true");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Użytkownik wylogował się");
        return Redirect("/login");
    }
}
