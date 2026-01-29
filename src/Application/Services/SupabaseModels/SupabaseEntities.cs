using DataAccess.Enums;
using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;

namespace KanbanLite.Application.Services.SupabaseModels;

[Table("batches")]
public class SupabaseBatch : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Reference(typeof(SupabaseProject))]
    public SupabaseProject? Project { get; set; }

    [Column("batch_no")]
    public int BatchNo { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("status")]
    public string? Status { get; set; } // Stored as string

    [Column("stage")]
    public short Stage { get; set; } // Stored as smallint

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("projects")]
public class SupabaseProject : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Reference(typeof(SupabaseOrder))]
    public SupabaseOrder? Order { get; set; }

    [Column("project_number")]
    public string? ProjectNumber { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("completed_by_user_id")]
    public string? CompletedByUserId { get; set; }

    [Reference(typeof(SupabaseBatch))]
    public List<SupabaseBatch> Batches { get; set; } = [];
}

[Table("orders")]
public class SupabaseOrder : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("order_number")]
    public string? OrderNumber { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("product_format_id")]
    public long ProductFormatId { get; set; }

    [Reference(typeof(SupabaseProductFormat))]
    public SupabaseProductFormat? ProductFormat { get; set; }

    [Column("due_date")]
    public DateTime DueDate { get; set; } // Using DateTime for DateOnly column

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("batch_audit_log")]
public class SupabaseBatchAuditLog : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("batch_id")]
    public long BatchId { get; set; }

    [Column("changed_at")]
    public DateTimeOffset ChangedAt { get; set; }

    [Column("changed_by_user_id")]
    public string? ChangedByUserId { get; set; }

    [Column("old_status")]
    public string? OldStatus { get; set; }

    [Column("new_status")]
    public string? NewStatus { get; set; }

    [Column("old_stage")]
    public short? OldStage { get; set; }

    [Column("new_stage")]
    public short? NewStage { get; set; }
}

[Table("product_formats")]
public class SupabaseProductFormat : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("batch_split_rules")]
public class SupabaseBatchSplitRule : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("min_qty")]
    public int MinQty { get; set; }

    [Column("max_qty")]
    public int? MaxQty { get; set; }

    [Column("percent")]
    public decimal Percent { get; set; }

    [Column("min_batch_size")]
    public int MinBatchSize { get; set; }

    [Column("max_batches_per_project")]
    public int? MaxBatchesPerProject { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
