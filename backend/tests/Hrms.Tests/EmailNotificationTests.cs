using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure.Persistence;
using Hrms.Infrastructure.Identity;
using System.Security.Cryptography;
using System.Text;
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
    public void Account_templates_use_secure_action_without_exposing_password()
    {
        var templates = EmailTemplateCatalog.CreateDefaults(Guid.NewGuid());
        var account = Assert.Single(templates, x => x.Key == EmailTemplateKeys.AccountCreated);
        var reset = Assert.Single(templates, x => x.Key == EmailTemplateKeys.PasswordReset);

        Assert.DoesNotContain("{{temporaryPassword}}", account.HtmlTemplate);
        Assert.Contains("{{actionUrl}}", account.HtmlTemplate);
        Assert.DoesNotContain("{{temporaryPassword}}", reset.TextTemplate);
        Assert.Contains("{{actionUrl}}", reset.TextTemplate);
    }

    [Fact]
    public void Only_credential_emails_get_expiring_single_use_links()
    {
        Assert.True(EmailTemplateKeys.RequiresExpiringActionLink(EmailTemplateKeys.AccountCreated));
        Assert.True(EmailTemplateKeys.RequiresExpiringActionLink(EmailTemplateKeys.PasswordReset));
        Assert.False(EmailTemplateKeys.RequiresExpiringActionLink(EmailTemplateKeys.ForNotification("work")));
        Assert.False(EmailTemplateKeys.RequiresExpiringActionLink(EmailTemplateKeys.ForNotification("leave")));
    }

    [Fact]
    public async Task Ordinary_email_link_can_be_opened_repeatedly()
    {
        var tenant = new MutableTenant();
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, new TestCurrentUser(), new TestNotificationPublisher());
        const string token = "reusable-email-test-token";
        var userId = Guid.NewGuid();
        db.EmailSignInLinks.Add(new EmailSignInLink
        {
            TenantId = tenant.TenantId!.Value, UserId = userId, RecipientEmail = "employee@example.test",
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            Destination = "/work?item=123", ExpiresAt = null
        });
        await db.SaveChangesAsync();

        var store = new EmailSignInLinkStore(db);
        Assert.Equal(tenant.TenantId, await store.FindTenantIdAsync(token, default));
        Assert.Null(await store.ConsumeAsync(token, Guid.NewGuid(), default));
        Assert.Equal("/work?item=123", (await store.ConsumeAsync(token, tenant.TenantId!.Value, default))?.Destination);
        Assert.Equal(userId, (await store.ConsumeAsync(token, tenant.TenantId!.Value, default))?.UserId);
        Assert.Null((await db.EmailSignInLinks.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task Account_link_cannot_be_consumed_from_another_company()
    {
        var tenant = new MutableTenant();
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, new TestCurrentUser(), new TestNotificationPublisher());
        const string token = "account-link-token";
        db.EmailSignInLinks.Add(new EmailSignInLink
        {
            TenantId = tenant.TenantId!.Value, UserId = Guid.NewGuid(), RecipientEmail = "employee@example.test",
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            Destination = "/my", ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var store = new EmailSignInLinkStore(db);
        Assert.Null(await store.ConsumeAsync(token, Guid.NewGuid(), default));
        Assert.Null((await db.EmailSignInLinks.SingleAsync()).UsedAt);
        Assert.Equal(tenant.TenantId, await store.FindTenantIdAsync(token, default));
    }

    [Theory]
    [InlineData("/work?project=abc&item=def", "/work?project=abc&item=def")]
    [InlineData("https://malicious.example/path", "/dashboard")]
    [InlineData("//malicious.example/path", "/dashboard")]
    [InlineData("/\\malicious.example", "/dashboard")]
    public void Email_link_destination_stays_inside_application(string requested, string expected)
    {
        Assert.Equal(expected, Hrms.Infrastructure.EmailOutboxWorker.SafeDestination(requested, "/dashboard"));
    }

    [Fact]
    public void Account_activation_link_opens_password_setup_without_requiring_login()
    {
        const string baseUrl = "https://acme.hrms.example";
        const string activationPath = "/activate?token=activation-token";

        Assert.Equal(
            "https://acme.hrms.example/activate?token=activation-token",
            Hrms.Infrastructure.EmailOutboxWorker.BuildActionUrl(baseUrl, activationPath, EmailTemplateKeys.AccountActivation));
        Assert.Equal(
            "https://acme.hrms.example/login?returnUrl=%2Factivate%3Ftoken%3Dactivation-token",
            Hrms.Infrastructure.EmailOutboxWorker.BuildActionUrl(baseUrl, activationPath));
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
