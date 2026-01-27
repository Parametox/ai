using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanLite.Web.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
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

        var result = await _authService.LoginAsync(username, password);

        if (result == null)
        {
            return Redirect("/login?error=true");
        }

        var redirectUrl = !string.IsNullOrEmpty(returnUrl) && returnUrl != "/"
            ? returnUrl
            : result;

        return Redirect(redirectUrl);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return Redirect("/login");
    }
}
