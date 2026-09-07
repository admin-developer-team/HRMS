using System.Linq.Expressions;
using Hrms.Domain.Common;

namespace Hrms.Application;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    string? Slug { get; }
    void Set(Guid tenantId, string? slug = null);
    void Clear();
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? EmployeeId { get; }
    bool IsPlatformAdmin { get; }
    bool HasPermission(string permission);
}

public interface IRepository<T> where T : AuditableEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

public interface ITokenService
{
    string CreateAccessToken(Guid userId, Guid tenantId, Guid? employeeId, string email, bool isPlatformAdmin, IEnumerable<string> roles, IEnumerable<string> permissions, Guid? sessionId = null);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
    int RefreshTokenLifetimeDays { get; }
    int AccessTokenLifetimeMinutes { get; }
}

public interface IAuditReader
{
    Task<PagedResult<AuditLogDto>> SearchAsync(PagedRequest request, string? entityType, CancellationToken cancellationToken);
}
