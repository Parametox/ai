using DataAccess.Identity;

namespace DataAccess.Entities;

public sealed class Project
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string ProjectNumber { get; set; } = null!;

    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public string? CompletedByUserId { get; set; }
    public ApplicationUser? CompletedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
}

