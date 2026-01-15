namespace DataAccess.Entities;

public sealed class ProductFormat
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

