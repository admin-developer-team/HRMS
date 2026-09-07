using Hrms.Application;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

var configuration = new ConfigurationBuilder().AddUserSecrets("hrms-backend-local-development").AddEnvironmentVariables().Build();
var connection = PostgresConnectionString.Normalize(configuration.GetConnectionString("Hrms") ?? throw new InvalidOperationException("Configure ConnectionStrings:Hrms using user-secrets or environment variables."));
await using var sql = new NpgsqlConnection(connection);
await sql.OpenAsync();
await using var check = new NpgsqlCommand("SELECT count(*) FROM (SELECT 1 FROM \"AttendanceRecords\" WHERE NOT \"IsDeleted\" AND \"ClockedOutAt\" IS NULL GROUP BY \"TenantId\", \"EmployeeId\" HAVING count(*) > 1) duplicates", sql);
var duplicates = (long)(await check.ExecuteScalarAsync())!;
Console.WriteLine($"Database connection verified. Employees with duplicate open sessions: {duplicates}.");
if (duplicates > 0) throw new InvalidOperationException("Resolve duplicate open sessions before applying the uniqueness migration; no records were changed.");
var tenant = new CurrentTenant();
await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>().UseNpgsql(connection).Options, tenant, new NoUser(), new NoNotifications());
var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
Console.WriteLine($"Pending migrations: {string.Join(", ", pending)}");
if (args.Contains("--migrate")) { await db.Database.MigrateAsync(); Console.WriteLine("Migrations applied successfully."); }

sealed class NoUser : ICurrentUser { public Guid? UserId => null; public Guid? EmployeeId => null; public bool IsPlatformAdmin => false; public bool HasPermission(string _) => false; }
sealed class NoNotifications : INotificationPublisher { public Task PublishChangedAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.CompletedTask; }
