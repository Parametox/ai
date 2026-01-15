namespace KanbanLite.Application.Common;

public sealed record AppError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null
)
{
    public static AppError ValidationFailed(
        string message,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null)
        => new("ValidationFailed", message, fieldErrors);

    public static AppError NotFound(string message) => new("NotFound", message);
    public static AppError Conflict(string message) => new("Conflict", message);
    public static AppError Unauthorized(string message) => new("Unauthorized", message);
    public static AppError Forbidden(string message) => new("Forbidden", message);
    public static AppError Unexpected(string message) => new("Unexpected", message);
}

