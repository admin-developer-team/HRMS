using Hrms.Application;
using Hrms.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hrms.Infrastructure.Persistence;

public sealed class HrmsDbContextFactory : IDesignTimeDbContextFactory<HrmsDbContext>
{
    public HrmsDbContext CreateDbContext(string[] args)
    {
        var configuredConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Hrms") ?? "Host=localhost;Port=5432;Database=hrms;Username=postgres;Password=postgres";
        var connection = PostgresConnectionString.Normalize(configuredConnection);
        var options = new DbContextOptionsBuilder<HrmsDbContext>().UseNpgsql(connection).Options;
        return new HrmsDbContext(options, new CurrentTenant(), new CurrentUser(new HttpContextAccessor()), new NullNotificationPublisher());
    }

    private sealed class NullNotificationPublisher : INotificationPublisher
    {
        public Task PublishChangedAsync(IEnumerable<Guid> userIds, CancellationToken ct) => Task.CompletedTask;
    }
}
