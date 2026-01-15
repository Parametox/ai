namespace DataAccess.Entities;

public sealed class BatchSplitRule
{
    public long Id { get; set; }
    public int MinQty { get; set; }
    public int? MaxQty { get; set; }
    public decimal Percent { get; set; }
    public int MinBatchSize { get; set; } = 1;
    public int? MaxBatchesPerProject { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

