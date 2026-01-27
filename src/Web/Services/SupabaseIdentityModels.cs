using Postgrest.Attributes;
using Postgrest.Models;

namespace KanbanLite.Web.Services;

// PostgREST/Supabase defaults to lower case, but if EF Core created quotes "AspNetUsers", we must match exact case if PostgREST respects it.
// However, Postgrest-csharp client typically uses the table name from the attribute.
// If the table is "AspNetUsers", we should try to match it.
[Table("AspNetUsers")]
public class SupabaseUser : BaseModel
{
    [PrimaryKey("Id", false)]
    public string Id { get; set; } = default!;

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Email")]
    public string? Email { get; set; }

    [Column("PasswordHash")]
    public string? PasswordHash { get; set; }
}

[Table("AspNetRoles")]
public class SupabaseRole : BaseModel
{
    [PrimaryKey("Id", false)]
    public string Id { get; set; } = default!;

    [Column("Name")]
    public string? Name { get; set; }
}

[Table("AspNetUserRoles")]
public class SupabaseUserRole : BaseModel
{
    [Column("UserId")]
    public string UserId { get; set; } = default!;

    [Column("RoleId")]
    public string RoleId { get; set; } = default!;
}
