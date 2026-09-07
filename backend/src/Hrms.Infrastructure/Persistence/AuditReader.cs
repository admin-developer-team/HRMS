using Hrms.Application;
using Hrms.Domain;

namespace Hrms.Infrastructure.Persistence;

public sealed class AuditReader(IRepository<AuditLog> logs) : IAuditReader
{
    public async Task<PagedResult<AuditLogDto>> SearchAsync(PagedRequest r, string? entityType, CancellationToken ct)
    {
        System.Linq.Expressions.Expression<Func<AuditLog, bool>> p = x => string.IsNullOrEmpty(entityType) || x.EntityType == entityType;
        var total = await logs.CountAsync(p, ct);
        var rows = await logs.ListAsync(p, q => q.OrderByDescending(x => x.CreatedAt), r.Skip, r.SafePageSize, ct);
        return new(rows.Select(x => new AuditLogDto(x.Id, x.CreatedAt, x.ActorUserId, x.Action, x.EntityType, x.EntityId, x.IpAddress, x.CorrelationId)).ToArray(), r.SafePage, r.SafePageSize, total);
    }
}
