using Hrms.Domain;

namespace Hrms.Application;

public sealed record NotificationDto(Guid Id, string Title, string Message, string Kind, string? Link,
    bool IsRead, DateTimeOffset CreatedAt);
public sealed record NotificationFeedDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public interface INotificationPublisher
{
    Task PublishChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct);
}

public interface INotificationService
{
    Task<NotificationFeedDto> GetFeedAsync(int take, CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
    Task QueueForEmployeesAsync(IEnumerable<Guid?> employeeIds, string title, string message, string kind, string? link, CancellationToken ct);
    Task QueueForUsersAsync(IEnumerable<Guid> userIds, string title, string message, string kind, string? link, CancellationToken ct);
    Task QueueForPermissionAsync(string permission, string title, string message, string kind, string? link, CancellationToken ct);
    Task QueueForAllUsersAsync(string title, string message, string kind, string? link, CancellationToken ct);
}

public sealed class NotificationService(
    IRepository<UserNotification> notifications,
    IRepository<Employee> employees,
    IRepository<UserAccount> users,
    IRepository<Role> roles,
    IRepository<UserRole> userRoles,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork unitOfWork) : INotificationService
{
    private Guid UserId => user.UserId ?? throw new UnauthorizedAccessException("User identity is missing.");
    private Guid TenantId => tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant identity is missing.");

    public async Task<NotificationFeedDto> GetFeedAsync(int take, CancellationToken ct)
    {
        var rows = await notifications.ListAsync(x => x.UserId == UserId,
            x => x.OrderByDescending(n => n.CreatedAt), take: Math.Clamp(take, 1, 100), cancellationToken: ct);
        var unread = await notifications.CountAsync(x => x.UserId == UserId && x.ReadAt == null, ct);
        return new(rows.Select(Map).ToArray(), unread);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var row = await notifications.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Notification not found.");
        if (row.UserId != UserId) throw new UnauthorizedAccessException("This notification belongs to another user.");
        row.ReadAt ??= DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var rows = await notifications.ListAsync(x => x.UserId == UserId && x.ReadAt == null, cancellationToken: ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows) row.ReadAt = now;
        if (rows.Count > 0) await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task QueueForEmployeesAsync(IEnumerable<Guid?> employeeIds, string title, string message, string kind, string? link, CancellationToken ct)
    {
        var ids = employeeIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (ids.Length == 0) return;
        var recipients = await employees.ListAsync(x => ids.Contains(x.Id) && x.UserId != null, cancellationToken: ct);
        foreach (var recipientId in recipients.Select(x => x.UserId!.Value).Distinct().Where(x => x != user.UserId))
            await notifications.AddAsync(new UserNotification
            {
                TenantId = TenantId, UserId = recipientId, Title = title.Trim(), Message = message.Trim(),
                Kind = string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim().ToLowerInvariant(), Link = link
            }, ct);
    }

    public async Task QueueForUsersAsync(IEnumerable<Guid> userIds, string title, string message, string kind, string? link, CancellationToken ct)
    {
        var ids = userIds.Distinct().Where(x => x != user.UserId).ToArray();
        if (ids.Length == 0) return;
        var recipients = await users.ListAsync(x => ids.Contains(x.Id) && x.IsActive, cancellationToken: ct);
        foreach (var recipientId in recipients.Select(x => x.Id))
            await notifications.AddAsync(new UserNotification
            {
                TenantId = TenantId, UserId = recipientId, Title = title.Trim(), Message = message.Trim(),
                Kind = string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim().ToLowerInvariant(), Link = link
            }, ct);
    }

    public async Task QueueForPermissionAsync(string permission, string title, string message, string kind, string? link, CancellationToken ct)
    {
        var matchingRoles = (await roles.ListAsync(cancellationToken: ct))
            .Where(c => c.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => x == Permissions.All || string.Equals(x, permission, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (matchingRoles.Length == 0) return;
        var roleIds = matchingRoles.Select(x => x.Id).ToArray();
        var recipientUserIds = (await userRoles.ListAsync(x => roleIds.Contains(x.RoleId), cancellationToken: ct))
            .Select(x => x.UserId).Distinct().ToArray();
        if (recipientUserIds.Length == 0) return;
        var recipients = await users.ListAsync(x => recipientUserIds.Contains(x.Id) && x.IsActive, cancellationToken: ct);
        foreach (var recipientId in recipients.Select(x => x.Id).Distinct().Where(x => x != user.UserId))
            await notifications.AddAsync(new UserNotification
            {
                TenantId = TenantId, UserId = recipientId, Title = title.Trim(), Message = message.Trim(),
                Kind = string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim().ToLowerInvariant(), Link = link
            }, ct);
    }

    public async Task QueueForAllUsersAsync(string title, string message, string kind, string? link, CancellationToken ct)
    {
        var recipients = await users.ListAsync(x => x.IsActive, cancellationToken: ct);
        foreach (var recipientId in recipients.Select(x => x.Id).Distinct().Where(x => x != user.UserId))
            await notifications.AddAsync(new UserNotification
            {
                TenantId = TenantId, UserId = recipientId, Title = title.Trim(), Message = message.Trim(),
                Kind = string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim().ToLowerInvariant(), Link = link
            }, ct);
    }

    private static NotificationDto Map(UserNotification x) => new(x.Id, x.Title, x.Message, x.Kind, x.Link, x.ReadAt.HasValue, x.CreatedAt);
}
