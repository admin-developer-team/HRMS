using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class PlatformExperienceTests
{
    [Fact]
    public async Task Trial_starts_only_after_password_activation_and_queues_workspace_email()
    {
        var tenant = new CurrentTenant(); tenant.Set(DatabaseInitializer.PlatformTenantId, "platform");
        await using var db = CreateDb(tenant);
        db.Tenants.Add(new Tenant { Id = DatabaseInitializer.PlatformTenantId, Slug = "platform", Name = "PeopleFlow", Status = TenantStatus.Active });
        await db.SaveChangesAsync();
        var service = Service(db, tenant);

        await service.RequestTrialAsync(
            new("Acme Studio", "acme-studio", "Alex Morgan", "alex@example.test", "starter"),
            "http://localhost:4200",
            default);

        var company = await db.Tenants.IgnoreQueryFilters().SingleAsync(x => x.Slug == "acme-studio");
        Assert.Null(company.TrialEndsAt);
        Assert.Equal("starter", company.PreferredPlanCode);
        var admin = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.TenantId == company.Id);
        Assert.False(admin.IsActive);
        var email = await db.EmailOutboxItems.IgnoreQueryFilters().SingleAsync(x => x.ToEmail == admin.Email);
        Assert.Equal(EmailTemplateKeys.AccountActivation, email.TemplateKey);
        var emailModel = JsonSerializer.Deserialize<Dictionary<string, string>>(email.ModelJson)!;
        Assert.Equal("http://localhost:4200", emailModel["applicationBaseUrl"]);
        var link = emailModel["link"];
        Assert.Equal(
            $"http://acme-studio.localhost:4200{link}",
            emailModel["actionUrl"]);
        var emailBaseUrl = EmailOutboxWorker.ResolveTenantBaseUrl(
            "https://hrms.ssym.co.in",
            emailModel["applicationBaseUrl"],
            "hrms.ssym.co.in",
            company.Slug);
        Assert.Equal(
            $"http://acme-studio.localhost:4200{link}",
            EmailOutboxWorker.BuildActionUrl(emailBaseUrl, link!, email.TemplateKey));
        var token = link!.Split("token=")[1];

        tenant.Set(company.Id, company.Slug);
        await service.ActivateAsync(new(token, "A-strong-password-123", null, null), default);

        Assert.True(admin.IsActive);
        Assert.InRange(company.TrialEndsAt!.Value, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
        Assert.True((await db.TenantSubscriptions.SingleAsync()).IsActive);
        await Assert.ThrowsAsync<Hrms.Domain.Common.DomainException>(() => service.ActivateAsync(new(token, "A-strong-password-123", null, null), default));
    }

    [Fact]
    public async Task Support_request_is_saved_and_notifies_enabled_team_without_email_delivery()
    {
        var tenant = new CurrentTenant(); tenant.Set(DatabaseInitializer.PlatformTenantId, "platform");
        await using var db = CreateDb(tenant);
        db.Tenants.Add(new Tenant { Id = DatabaseInitializer.PlatformTenantId, Slug = "platform", Name = "PeopleFlow", Status = TenantStatus.Active });
        var role = new Role { TenantId = DatabaseInitializer.PlatformTenantId, Name = "Support", NormalizedName = "SUPPORT_AGENT", PermissionsCsv = $"{Permissions.SupportRead},{Permissions.SupportManage}" };
        var enabled = new UserAccount { TenantId = DatabaseInitializer.PlatformTenantId, DisplayName = "Enabled", Email = "enabled@example.test", IsActive = true, SupportInboxEnabled = true };
        var disabled = new UserAccount { TenantId = DatabaseInitializer.PlatformTenantId, DisplayName = "Disabled", Email = "disabled@example.test", IsActive = true, SupportInboxEnabled = false };
        db.Roles.Add(role); db.Users.AddRange(enabled, disabled);
        db.UserRoles.AddRange(new UserRole { TenantId = DatabaseInitializer.PlatformTenantId, UserId = enabled.Id, RoleId = role.Id }, new UserRole { TenantId = DatabaseInitializer.PlatformTenantId, UserId = disabled.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        var reference = await Service(db, tenant).CreateTicketAsync(new("Customer", "customer@example.test", "technical", "Cannot sign in", "I cannot sign in to the workspace today."), default);

        Assert.StartsWith("PF-", reference);
        Assert.Equal(reference, (await db.SupportTickets.SingleAsync()).Reference);
        Assert.Equal(2, await db.EmailOutboxItems.CountAsync());
        Assert.Single(await db.UserNotifications.ToListAsync());
        Assert.Equal(enabled.Id, (await db.UserNotifications.SingleAsync()).UserId);
    }

    private static PlatformExperienceService Service(HrmsDbContext db, CurrentTenant tenant) =>
        new(db, tenant, new TestCurrentUser { IsPlatformAdmin = true }, new Pbkdf2PasswordHasher());

    private static HrmsDbContext CreateDb(CurrentTenant tenant)
    {
        var options = new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new HrmsDbContext(options, tenant, new TestCurrentUser { IsPlatformAdmin = true }, new TestNotificationPublisher());
    }
}
