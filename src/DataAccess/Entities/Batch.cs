using DataAccess.Enums;

namespace DataAccess.Entities;

public sealed class Batch
{
    public long Id { get; set; }

    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int BatchNo { get; set; }
    public int Quantity { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.New;
    public ProductionStage Stage { get; set; } = ProductionStage.Design;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<BatchAuditLog> AuditLog { get; set; } = new List<BatchAuditLog>();
}

