using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataAccess.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;
    
    private IRepository<Order>? _orders;
    private IRepository<Project>? _projects;
    private IRepository<Batch>? _batches;
    private IRepository<BatchAuditLog>? _batchAuditLogs;
    private IRepository<ProductFormat>? _productFormats;
    private IRepository<BatchSplitRule>? _batchSplitRules;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<Order> Orders => _orders ??= new Repository<Order>(_context);
    public IRepository<Project> Projects => _projects ??= new Repository<Project>(_context);
    public IRepository<Batch> Batches => _batches ??= new Repository<Batch>(_context);
    public IRepository<BatchAuditLog> BatchAuditLogs => _batchAuditLogs ??= new Repository<BatchAuditLog>(_context);
    public IRepository<ProductFormat> ProductFormats => _productFormats ??= new Repository<ProductFormat>(_context);
    public IRepository<BatchSplitRule> BatchSplitRules => _batchSplitRules ??= new Repository<BatchSplitRule>(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (SupportsTransactions())
        {
            _transaction = await _context.Database.BeginTransactionAsync(ct);
        }
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public bool SupportsTransactions()
    {
        return !string.Equals(
            _context.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
