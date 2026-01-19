using KanbanLite.Application.Common;
using DataAccess.Identity;

namespace KanbanLite.Web.Services;

public interface IAuthService
{
    Task<Result<LoginResult>> LoginAsync(string username, string password, bool isPersistent = false);
    Task LogoutAsync();
}

public record LoginResult(string UserId, string Username, IReadOnlyList<string> Roles, ApplicationUser User);
