using Hrms.Application;
using Hrms.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hrms.Infrastructure.Persistence;

public sealed class DatabaseInitializer(HrmsDbContext db, ICurrentTenant currentTenant, IPasswordHasher passwordHasher, IConfiguration configuration, IHostEnvironment environment)
{
    public static readonly Guid PlatformTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlatformUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PlatformRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == PlatformTenantId, ct))
        {
            var password = configuration["Bootstrap:PlatformAdminPassword"];
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || (!environment.IsDevelopment() && password.StartsWith("CHANGE", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Set Bootstrap:PlatformAdminPassword to a secure value of at least 8 characters before first startup.");
            var email = configuration["Bootstrap:PlatformAdminEmail"] ?? "platform-admin@local.invalid";
            db.Tenants.Add(new Tenant { Id = PlatformTenantId, Name = "HRMS Platform", Slug = "platform", Status = TenantStatus.Active, DefaultCurrency = "USD", TimeZone = "UTC" });
            currentTenant.Set(PlatformTenantId, "platform");
            db.Roles.Add(new Role { Id = PlatformRoleId, TenantId = PlatformTenantId, Name = "Platform Administrator", NormalizedName = "PLATFORM_ADMIN", PermissionsCsv = "*", IsSystem = true });
            db.Users.Add(new UserAccount { Id = PlatformUserId, TenantId = PlatformTenantId, Email = email.Trim().ToLowerInvariant(), DisplayName = "Platform Administrator", PasswordHash = passwordHasher.Hash(password), IsActive = true, IsPlatformAdmin = true });
            db.UserRoles.Add(new UserRole { TenantId = PlatformTenantId, UserId = PlatformUserId, RoleId = PlatformRoleId });
            await db.SaveChangesAsync(ct);
        }
        await EnsurePlatformEmailSettingsAsync(ct);
        var tenants = await db.Tenants.IgnoreQueryFilters().Where(x => x.Id != PlatformTenantId).ToListAsync(ct);
        foreach (var tenant in tenants)
        {
            currentTenant.Set(tenant.Id, tenant.Slug);
            foreach (var definition in Permissions.TenantSystemRoles)
            {
                if (await db.Roles.AnyAsync(x => x.NormalizedName == definition.NormalizedName, ct)) continue;
                db.Roles.Add(new Role { TenantId = tenant.Id, Name = definition.Name, NormalizedName = definition.NormalizedName, PermissionsCsv = string.Join(',', definition.Permissions), IsSystem = true });
            }
            var legacyEmployeeRole = await db.Roles.FirstOrDefaultAsync(x => x.NormalizedName == "EMPLOYEE", ct);
            if (legacyEmployeeRole is not null && legacyEmployeeRole.PermissionsCsv.Contains("hr.manage", StringComparison.OrdinalIgnoreCase))
                legacyEmployeeRole.PermissionsCsv = Permissions.SelfService;
            await db.SaveChangesAsync(ct);
        }
        currentTenant.Clear();
    }

    private async Task EnsurePlatformEmailSettingsAsync(CancellationToken ct)
    {
        currentTenant.Set(PlatformTenantId, "platform");
        var allConfigurations = await db.EmailConfigurations.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted).OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(ct);
        var platformConfiguration = allConfigurations.FirstOrDefault(x => x.TenantId == PlatformTenantId);
        if (platformConfiguration is null)
        {
            var previous = allConfigurations.FirstOrDefault(x => x.IsEnabled);
            if (previous is not null)
            {
                db.EmailConfigurations.Add(new EmailConfiguration
                {
                    TenantId = PlatformTenantId,
                    IsEnabled = previous.IsEnabled,
                    Host = previous.Host,
                    Port = previous.Port,
                    Username = previous.Username,
                    EncryptedPassword = previous.EncryptedPassword,
                    UseTls = previous.UseTls,
                    FromEmail = previous.FromEmail,
                    FromName = previous.FromName,
                    ReplyToEmail = previous.ReplyToEmail,
                    ApplicationBaseUrl = previous.ApplicationBaseUrl
                });
            }
        }
        foreach (var obsolete in allConfigurations.Where(x => x.TenantId != PlatformTenantId && x.IsEnabled))
            obsolete.IsEnabled = false;

        var existingKeys = await db.EmailTemplates.IgnoreQueryFilters()
            .Where(x => x.TenantId == PlatformTenantId && !x.IsDeleted).Select(x => x.Key).ToListAsync(ct);
        var keySet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        db.EmailTemplates.AddRange(EmailTemplateCatalog.CreateDefaults(PlatformTenantId).Where(x => !keySet.Contains(x.Key)));
        await db.SaveChangesAsync(ct);
    }
}
