using DataAccess.Enums;
using DataAccess.Identity;

namespace DataAccess.Entities;

public sealed class BatchAuditLog
{
    public long Id { get; set; }

    public long BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public DateTimeOffset ChangedAt { get; set; }

    public string ChangedByUserId { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;

    public BatchStatus? OldStatus { get; set; }
    public BatchStatus? NewStatus { get; set; }

    public ProductionStage? OldStage { get; set; }
    public ProductionStage? NewStage { get; set; }
}

