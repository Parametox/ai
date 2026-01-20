namespace KanbanLite.Web.Services;

public interface IAuthService
{
    Task<string?> LoginAsync(string username, string password);
    Task LogoutAsync();
}
