using DataAccess.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Web.Services;

public sealed class AuthService : IAuthService
{
    private readonly Supabase.Client _supabaseClient;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ISessionService _sessionService;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        Supabase.Client supabaseClient,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ISessionService sessionService,
        AuthenticationStateProvider authenticationStateProvider,
        ILogger<AuthService> logger)
    {
        _supabaseClient = supabaseClient;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
        _authenticationStateProvider = authenticationStateProvider;
        _logger = logger;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("Próba logowania z pustą nazwą użytkownika");
                return null;
            }

            var response = await _supabaseClient.From<SupabaseUser>()
                .Select("*")
                .Filter("UserName", Operator.Like, username)
                .Get();

            var user = response.Models.FirstOrDefault();

            if (user == null)
            {
                _logger.LogWarning("Nieudana próba logowania - użytkownik nie istnieje: {Username}", username);
                return null;
            }

            // Hasło jest opcjonalne - jeśli użytkownik ma ustawione hasło, weryfikujemy je
            // Jeśli nie ma hasła w bazie lub hasło nie zostało podane, pomijamy weryfikację
            if (!string.IsNullOrEmpty(user.PasswordHash) && !string.IsNullOrWhiteSpace(password))
            {
                var appUser = new ApplicationUser { Id = user.Id, UserName = user.UserName, PasswordHash = user.PasswordHash };
                var verificationResult = _passwordHasher.VerifyHashedPassword(appUser, user.PasswordHash, password);
                
                if (verificationResult == PasswordVerificationResult.Failed)
                {
                    _logger.LogWarning("Nieudana próba logowania - nieprawidłowe hasło dla użytkownika: {Username}", username);
                    return null;
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

            // Ustawienie sesji w SessionService
            _sessionService.SetSession(user.Id, user.UserName ?? username, roles);

            // Powiadom o zmianie stanu autoryzacji
            if (_authenticationStateProvider is RevalidatingIdentityAuthenticationStateProvider<ApplicationUser> customProvider)
            {
                customProvider.NotifyAuthenticationStateChanged();
            }

            //_logger.LogInformation("Użytkownik {Username} zalogował się pomyślnie", username);

            return "/kanban";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas logowania użytkownika: {Username}", username);
            throw;
        }
    }

    public Task LogoutAsync()
    {
        try
        {
            _sessionService.ClearSession();
            // Powiadom o zmianie stanu autoryzacji
            if (_authenticationStateProvider is RevalidatingIdentityAuthenticationStateProvider<ApplicationUser> customProvider)
            {
                customProvider.NotifyAuthenticationStateChanged();
            }
            _logger.LogInformation("Użytkownik wylogował się");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wylogowania użytkownika");
        }

        return Task.CompletedTask;
    }
}
