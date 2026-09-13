using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class AuditDisplayTests
{
    [Fact]
    public async Task Audit_reader_resolves_actor_display_name()
    {
        var tenant = new CurrentTenant();
        tenant.Set(Guid.NewGuid());
        var actor = new TestCurrentUser();
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, actor, new TestNotificationPublisher());
        db.Users.Add(new UserAccount { Id = actor.UserId!.Value, TenantId = tenant.TenantId!.Value,
            DisplayName = "Platform Administrator", Email = "admin@example.test", IsActive = true });
        db.AuditLogs.Add(new AuditLog { TenantId = tenant.TenantId.Value, ActorUserId = actor.UserId,
            Action = "Login", EntityType = "UserAccount", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var reader = new AuditReader(new Repository<AuditLog>(db), new Repository<UserAccount>(db));
        var result = await reader.SearchAsync(new PagedRequest(), null, default);

        Assert.Contains(result.Items, row => row.ActorName == "Platform Administrator");
    }
}
