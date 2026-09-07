using Hrms.Application;

namespace Hrms.Tests;

internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; init; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public Guid? EmployeeId { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public bool HasPermission(string permission) => IsPlatformAdmin;
}

internal sealed class TestNotificationPublisher : INotificationPublisher
{
    public List<Guid> PublishedUserIds { get; } = [];

    public Task PublishChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        PublishedUserIds.AddRange(userIds);
        return Task.CompletedTask;
    }
}
