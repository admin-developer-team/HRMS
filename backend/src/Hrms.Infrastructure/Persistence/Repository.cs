using System.Linq.Expressions;
using Hrms.Application;
using Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure.Persistence;

public sealed class Repository<T>(HrmsDbContext db) : IRepository<T> where T : AuditableEntity
{
    private readonly DbSet<T> _set = db.Set<T>();
    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) => _set.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _set.FirstOrDefaultAsync(predicate, ct);
    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, int? skip = null, int? take = null, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _set;
        if (predicate is not null) query = query.Where(predicate);
        if (orderBy is not null) query = orderBy(query);
        if (skip.HasValue) query = query.Skip(skip.Value);
        if (take.HasValue) query = query.Take(take.Value);
        return await query.ToListAsync(cancellationToken);
    }
    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default) => predicate is null ? _set.CountAsync(cancellationToken) : _set.CountAsync(predicate, cancellationToken);
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => _set.AnyAsync(predicate, cancellationToken);
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) => await _set.AddAsync(entity, cancellationToken);
    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);
}
