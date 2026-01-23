using DataAccess.Entities;

namespace DataAccess.Repositories;

public interface IUnitOfWork : IDisposable
{
    IRepository<Order> Orders { get; }
    IRepository<Project> Projects { get; }
    IRepository<Batch> Batches { get; }
    IRepository<BatchAuditLog> BatchAuditLogs { get; }
    IRepository<ProductFormat> ProductFormats { get; }
    IRepository<BatchSplitRule> BatchSplitRules { get; }
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
    bool SupportsTransactions();
}
