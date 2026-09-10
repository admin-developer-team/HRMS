using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class EmailNotificationTests
{
    [Fact]
    public async Task In_app_notification_also_creates_templated_email_outbox_item_when_enabled()
    {
        var tenant = new MutableTenant();
        var currentUser = new TestCurrentUser();
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, currentUser, new TestNotificationPublisher());
        var recipient = new UserAccount
        {
            TenantId = tenant.TenantId!.Value, Email = "employee@example.test", DisplayName = "Employee", IsActive = true
        };
        db.Users.Add(recipient);
        await db.SaveChangesAsync();

        var queue = new EmailQueue(new FixedGlobalEmailConfigurationReader(true), new Repository<EmailOutboxItem>(db), tenant);
        var notifications = new NotificationService(new Repository<UserNotification>(db), new Repository<Employee>(db),
            new Repository<UserAccount>(db), new Repository<Role>(db), new Repository<UserRole>(db), tenant,
            currentUser, db, queue);

        await notifications.QueueForUsersAsync([recipient.Id], "Leave approved", "Your leave was approved.", "leave", "/my-services", default);
        await db.SaveChangesAsync();

        var email = await db.EmailOutboxItems.SingleAsync();
        Assert.Equal(recipient.Email, email.ToEmail);
        Assert.Equal("notification.leave", email.TemplateKey);
        Assert.Contains("Leave approved", email.ModelJson);
        Assert.Single(await db.UserNotifications.ToListAsync());
    }

    [Fact]
    public async Task Queue_is_noop_when_platform_email_is_disabled()
    {
        var tenant = new MutableTenant();
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, new TestCurrentUser(), new TestNotificationPublisher());
        var queue = new EmailQueue(new FixedGlobalEmailConfigurationReader(false), new Repository<EmailOutboxItem>(db), tenant);

        await queue.QueueAsync("employee@example.test", "Employee", "notification.leave", new Dictionary<string, string?>(), default);
        await db.SaveChangesAsync();

        Assert.Empty(await db.EmailOutboxItems.ToListAsync());
    }

    [Fact]
    public void Credential_templates_include_temporary_password_and_company_login_action()
    {
        var templates = EmailTemplateCatalog.CreateDefaults(Guid.NewGuid());
        var account = Assert.Single(templates, x => x.Key == EmailTemplateKeys.AccountCreated);
        var reset = Assert.Single(templates, x => x.Key == EmailTemplateKeys.PasswordReset);

        Assert.Contains("{{temporaryPassword}}", account.HtmlTemplate);
        Assert.Contains("{{actionUrl}}", account.HtmlTemplate);
        Assert.Contains("{{temporaryPassword}}", reset.TextTemplate);
    }

    private sealed class MutableTenant : ICurrentTenant
    {
        public Guid? TenantId { get; private set; } = Guid.NewGuid();
        public string? Slug { get; private set; } = "test";
        public void Set(Guid tenantId, string? slug = null) { TenantId = tenantId; Slug = slug; }
        public void Clear() { TenantId = null; Slug = null; }
    }

    private sealed class FixedGlobalEmailConfigurationReader(bool enabled) : IGlobalEmailConfigurationReader
    {
        public Task<bool> IsEnabledAsync(CancellationToken ct) => Task.FromResult(enabled);
    }
}
