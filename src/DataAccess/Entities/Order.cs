namespace DataAccess.Entities;

public sealed class Order
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public int Quantity { get; set; }

    public long ProductFormatId { get; set; }
    public ProductFormat ProductFormat { get; set; } = null!;

    public DateOnly DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

