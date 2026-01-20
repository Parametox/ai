using KanbanLite.Application.Common;

namespace KanbanLite.Application.Security;

/// <summary>
/// Helper do wspólnej logiki autoryzacji w serwisach Application.
/// </summary>
public static class AuthorizationHelper
{
    private static readonly string[] ManagerAndOperatorRoles = ["Manager", "Operator"];

    /// <summary>
    /// Sprawdza czy użytkownik jest zalogowany i ma rolę Manager.
    /// </summary>
    public static AppError? EnsureManagerAuthorized(ICurrentUser currentUser, string? customForbiddenMessage = null)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return AppError.Unauthorized("Użytkownik nie jest zalogowany.");
        }

        return currentUser.IsInRole("Manager")
            ? null
            : AppError.Forbidden(customForbiddenMessage ?? "Brak uprawnień. Wymagana rola: Manager.");
    }

    /// <summary>
    /// Sprawdza czy użytkownik jest zalogowany i ma rolę Manager lub Operator.
    /// </summary>
    public static AppError? EnsureManagerOrOperatorAuthorized(ICurrentUser currentUser, string? customForbiddenMessage = null)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return AppError.Unauthorized("Użytkownik nie jest zalogowany.");
        }

        foreach (var role in ManagerAndOperatorRoles)
        {
            if (currentUser.IsInRole(role))
            {
                return null;
            }
        }

        return AppError.Forbidden(customForbiddenMessage ?? "Brak uprawnień. Wymagana rola: Manager lub Operator.");
    }

    /// <summary>
    /// Sprawdza czy użytkownik jest zalogowany (bez sprawdzania ról).
    /// </summary>
    public static AppError? EnsureAuthenticated(ICurrentUser currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? AppError.Unauthorized("Użytkownik nie jest zalogowany.")
            : null;
    }
}
