using DataAccess.Identity;
using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Postgrest;
using System.Security.Claims;
using static Postgrest.Constants;

namespace KanbanLite.Web.Controllers;

/// <summary>
/// Cookie Bridge Controller - wystawia ciasteczka autoryzacyjne przez HttpContext.SignInAsync.
/// Nie używa SignInManager aby uniknąć błędów współbieżności DbContext w Blazor Server.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public sealed class AuthController : ControllerBase
{
    private readonly Supabase.Client _supabaseClient;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        Supabase.Client supabaseClient,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger<AuthController> logger)
    {
        _supabaseClient = supabaseClient;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            _logger.LogWarning("Próba logowania z pustymi danymi");
            return BadRequest(new { error = "Username jest wymagane" });
        }

        try
        {
            // Weryfikacja użytkownika w Supabase
            var response = await _supabaseClient.From<SupabaseUser>()
                .Select("*")
                .Filter("UserName", Operator.Like, request.Username)
                .Get();

            var user = response.Models.FirstOrDefault();

            if (user == null)
            {
                _logger.LogWarning("Nieudana próba logowania - użytkownik nie istnieje: {Username}", request.Username);
                return Unauthorized(new { error = "Nieprawidłowe dane logowania" });
            }

            // Weryfikacja hasła (jeśli ustawione)
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                var appUser = new ApplicationUser { Id = user.Id, UserName = user.UserName, PasswordHash = user.PasswordHash };
                var verificationResult = _passwordHasher.VerifyHashedPassword(appUser, user.PasswordHash, request.Password);

                if (verificationResult == PasswordVerificationResult.Failed)
                {
                    _logger.LogWarning("Nieudana próba logowania - nieprawidłowe hasło dla użytkownika: {Username}", request.Username);
                    return Unauthorized(new { error = "Nieprawidłowe dane logowania" });
                }
            }

            // Pobierz role użytkownika
            var userRolesResponse = await _supabaseClient.From<SupabaseUserRole>()
                .Select("RoleId")
                .Filter("UserId", Operator.Equals, user.Id)
                .Get();

            var roleIds = userRolesResponse.Models.Select(ur => ur.RoleId).ToList();
            var roles = new List<string>();

            if (roleIds.Any())
            {
                var rolesResponse = await _supabaseClient.From<SupabaseRole>()
                    .Select("Name")
                    .Filter("Id", Operator.In, roleIds)
                    .Get();
                roles = rolesResponse.Models.Where(r => r.Name != null).Select(r => r.Name!).ToList();
            }

            // Stwórz claims dla ciasteczka
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? request.Username)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Wystawienie ciasteczka przez HttpContext.SignInAsync
            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true, // Trwałe ciasteczko (przetrwa zamknięcie przeglądarki)
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _logger.LogInformation("Użytkownik {Username} zalogował się pomyślnie", request.Username);

            return Ok(new
            {
                userId = user.Id,
                username = user.UserName ?? request.Username,
                roles = roles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas logowania użytkownika: {Username}", request.Username);
            return StatusCode(500, new { error = "Wystąpił błąd podczas logowania" });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            _logger.LogInformation("Użytkownik wylogował się");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wylogowania użytkownika");
            return StatusCode(500, new { error = "Wystąpił błąd podczas wylogowania" });
        }
    }
}

public record LoginRequest(string Username, string Password);
