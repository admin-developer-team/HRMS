using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public static class EmailTemplateKeys
{
    public const string DefaultNotification = "notification.default";
    public const string AccountCreated = "account.created";
    public const string PasswordReset = "account.password-reset";
    public const string CandidateApplicationReceived = "candidate.application-received";
    public const string CandidateStageChanged = "candidate.stage-changed";

    public static string ForNotification(string kind) => $"notification.{(string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim().ToLowerInvariant())}";

    public static readonly string[] NotificationKinds =
    [
        "leave", "attendance", "timesheet", "document", "announcement", "payroll", "recruitment",
        "performance", "asset", "expense", "training", "security", "work"
    ];
}

public static class EmailTemplateCatalog
{
    public static IReadOnlyList<EmailTemplate> CreateDefaults(Guid tenantId)
    {
        const string html = """
            <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#172033">
              <h2 style="color:#3157d5">{{title}}</h2><p>Hello {{recipientName}},</p>
              <p style="line-height:1.6">{{message}}</p>
              <p><a href="{{actionUrl}}" style="background:#3157d5;color:#fff;padding:11px 18px;border-radius:6px;text-decoration:none">Open PeopleFlow</a></p>
              <p style="color:#667085;font-size:12px">Sent by {{companyName}} through PeopleFlow HRMS.</p>
            </div>
            """;
        const string text = "{{title}}\n\nHello {{recipientName}},\n\n{{message}}\n\n{{actionUrl}}\n\nSent by {{companyName}} through PeopleFlow HRMS.";
        const string externalHtml = """
            <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#172033">
              <h2 style="color:#3157d5">{{title}}</h2><p>Hello {{recipientName}},</p>
              <p style="line-height:1.6">{{message}}</p>
              <p style="color:#667085;font-size:12px">Sent by {{companyName}} through PeopleFlow HRMS.</p>
            </div>
            """;
        var rows = new List<EmailTemplate>
        {
            New(tenantId, EmailTemplateKeys.DefaultNotification, "General notification", "{{companyName}} · {{title}}", html, text),
            New(tenantId, EmailTemplateKeys.AccountCreated, "Account created", "Your {{companyName}} HRMS account is ready",
                html.Replace("{{message}}", "Your PeopleFlow account has been created. Login email: <strong>{{email}}</strong><br>Temporary password: <strong>{{temporaryPassword}}</strong><br>Please change this password after signing in."),
                "Hello {{recipientName}},\n\nYour PeopleFlow account has been created.\nLogin email: {{email}}\nTemporary password: {{temporaryPassword}}\n\nPlease change this password after signing in.\n\n{{actionUrl}}"),
            New(tenantId, EmailTemplateKeys.PasswordReset, "Password reset", "Your {{companyName}} HRMS password was reset",
                html.Replace("{{message}}", "An administrator reset your password.<br>Login email: <strong>{{email}}</strong><br>Temporary password: <strong>{{temporaryPassword}}</strong><br>Please change this password after signing in."),
                "Hello {{recipientName}},\n\nAn administrator reset your password.\nLogin email: {{email}}\nTemporary password: {{temporaryPassword}}\n\nPlease change this password after signing in.\n\n{{actionUrl}}"),
            New(tenantId, EmailTemplateKeys.CandidateApplicationReceived, "Candidate application received", "Application received · {{jobTitle}}",
                externalHtml.Replace("{{message}}", "We received your application for {{jobTitle}} ({{jobCode}}). Our hiring team will contact you when there is an update."),
                "Hello {{recipientName}},\n\nWe received your application for {{jobTitle}} ({{jobCode}}). Our hiring team will contact you when there is an update.\n\n{{companyName}}"),
            New(tenantId, EmailTemplateKeys.CandidateStageChanged, "Candidate application status", "Application update · {{jobTitle}}",
                externalHtml.Replace("{{message}}", "Your application for {{jobTitle}} is now at the {{stage}} stage."),
                "Hello {{recipientName}},\n\nYour application for {{jobTitle}} is now at the {{stage}} stage.\n\n{{companyName}}")
        };
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["leave"] = "Leave", ["attendance"] = "Attendance", ["timesheet"] = "Timesheet",
            ["document"] = "Employee document", ["announcement"] = "Announcement", ["payroll"] = "Payroll",
            ["recruitment"] = "Recruitment", ["performance"] = "Performance", ["asset"] = "Asset",
            ["expense"] = "Expense", ["training"] = "Training", ["security"] = "Account security", ["work"] = "Work management"
        };
        rows.AddRange(EmailTemplateKeys.NotificationKinds.Select(kind =>
            New(tenantId, EmailTemplateKeys.ForNotification(kind), names[kind] + " notification", "{{companyName}} · {{title}}", html, text)));
        return rows;
    }

    private static EmailTemplate New(Guid tenantId, string key, string name, string subject, string html, string text) =>
        new() { TenantId = tenantId, Key = key, Name = name, SubjectTemplate = subject, HtmlTemplate = html, TextTemplate = text, IsEnabled = true, IsSystem = true };
}

public sealed record EmailConfigurationDto(bool IsEnabled, string Host, int Port, string? Username,
    bool HasPassword, bool UseTls, string FromEmail, string FromName, string? ReplyToEmail,
    string? ApplicationBaseUrl, long Version);
public sealed record UpdateEmailConfigurationRequest(bool IsEnabled, string Host, int Port, string? Username,
    string? Password, bool UseTls, string FromEmail, string FromName, string? ReplyToEmail,
    string? ApplicationBaseUrl, long Version);
public sealed record EmailTemplateDto(Guid Id, string Key, string Name, string SubjectTemplate,
    string HtmlTemplate, string? TextTemplate, bool IsEnabled, bool IsSystem, long Version);
public sealed record UpdateEmailTemplateRequest(string SubjectTemplate, string HtmlTemplate,
    string? TextTemplate, bool IsEnabled, long Version);
public sealed record SendTestEmailRequest(string? RecipientEmail);
public sealed record EmailDeliverySettings(string Host, int Port, string? Username, string? Password,
    bool UseTls, string FromEmail, string FromName, string? ReplyToEmail);
public sealed record RenderedEmail(string ToEmail, string? ToName, string Subject, string HtmlBody, string TextBody);

public interface IEmailSecretProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public interface IEmailTransport
{
    Task SendAsync(EmailDeliverySettings settings, RenderedEmail email, CancellationToken ct);
}

public interface IEmailQueue
{
    Task QueueAsync(string toEmail, string? toName, string templateKey, IReadOnlyDictionary<string, string?> model, CancellationToken ct);
}

public interface IGlobalEmailConfigurationReader
{
    Task<bool> IsEnabledAsync(CancellationToken ct);
}

public interface IEmailAdministrationService
{
    Task<EmailConfigurationDto> GetConfigurationAsync(CancellationToken ct);
    Task<EmailConfigurationDto> UpdateConfigurationAsync(UpdateEmailConfigurationRequest request, CancellationToken ct);
    Task<IReadOnlyList<EmailTemplateDto>> ListTemplatesAsync(CancellationToken ct);
    Task<EmailTemplateDto> UpdateTemplateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken ct);
    Task SendTestAsync(SendTestEmailRequest request, CancellationToken ct);
}

public sealed class EmailQueue(IGlobalEmailConfigurationReader configuration, IRepository<EmailOutboxItem> outbox,
    ICurrentTenant tenant) : IEmailQueue
{
    private Guid TenantId => tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant identity is missing.");

    public async Task QueueAsync(string toEmail, string? toName, string templateKey,
        IReadOnlyDictionary<string, string?> model, CancellationToken ct)
    {
        if (!await configuration.IsEnabledAsync(ct) || string.IsNullOrWhiteSpace(toEmail)) return;
        try { _ = new MailAddress(toEmail.Trim()); }
        catch (FormatException) { return; }
        await outbox.AddAsync(new EmailOutboxItem
        {
            TenantId = TenantId,
            ToEmail = toEmail.Trim().ToLowerInvariant(),
            ToName = string.IsNullOrWhiteSpace(toName) ? null : toName.Trim(),
            TemplateKey = templateKey.Trim().ToLowerInvariant(),
            ModelJson = JsonSerializer.Serialize(model),
            NextAttemptAt = DateTimeOffset.UtcNow
        }, ct);
    }
}

public sealed class EmailAdministrationService(
    IRepository<EmailConfiguration> configurations,
    IRepository<EmailTemplate> templates,
    IRepository<Tenant> tenants,
    IRepository<UserAccount> users,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork unitOfWork,
    IEmailSecretProtector protector,
    IEmailTransport transport) : IEmailAdministrationService
{
    private Guid TenantId => tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant identity is missing.");

    public async Task<EmailConfigurationDto> GetConfigurationAsync(CancellationToken ct) =>
        Map(await configurations.FirstOrDefaultAsync(x => true, ct));

    public async Task<EmailConfigurationDto> UpdateConfigurationAsync(UpdateEmailConfigurationRequest request, CancellationToken ct)
    {
        Validate(request.Host, request.Port, request.FromEmail, request.ReplyToEmail, request.ApplicationBaseUrl);
        if (request.IsEnabled && string.IsNullOrWhiteSpace(request.ApplicationBaseUrl))
            throw new DomainException("Application URL is required so email action links work.");
        var row = await configurations.FirstOrDefaultAsync(x => true, ct);
        if (row is null)
        {
            if (request.Version != 0) throw new DomainException("Email configuration was changed. Refresh and try again.");
            row = new EmailConfiguration { TenantId = TenantId };
            await configurations.AddAsync(row, ct);
        }
        else if (row.Version != request.Version) throw new DomainException("Email configuration was changed. Refresh and try again.");
        if (request.IsEnabled && !string.IsNullOrWhiteSpace(request.Username)
            && string.IsNullOrWhiteSpace(request.Password) && string.IsNullOrWhiteSpace(row.EncryptedPassword))
            throw new DomainException("An SMTP password or key is required when a username is configured.");
        row.IsEnabled = request.IsEnabled;
        row.Host = request.Host.Trim(); row.Port = request.Port;
        row.Username = NullIfBlank(request.Username); row.UseTls = request.UseTls;
        row.FromEmail = request.FromEmail.Trim().ToLowerInvariant(); row.FromName = request.FromName.Trim();
        row.ReplyToEmail = NullIfBlank(request.ReplyToEmail)?.ToLowerInvariant();
        row.ApplicationBaseUrl = NullIfBlank(request.ApplicationBaseUrl)?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(request.Password)) row.EncryptedPassword = protector.Protect(request.Password);
        await EnsureTemplatesAsync(ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<IReadOnlyList<EmailTemplateDto>> ListTemplatesAsync(CancellationToken ct)
    {
        if (await EnsureTemplatesAsync(ct)) await unitOfWork.SaveChangesAsync(ct);
        return (await templates.ListAsync(orderBy: q => q.OrderBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    }

    public async Task<EmailTemplateDto> UpdateTemplateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken ct)
    {
        var row = await templates.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Email template not found.");
        if (row.Version != request.Version) throw new DomainException("Email template was changed. Refresh and try again.");
        if (string.IsNullOrWhiteSpace(request.SubjectTemplate) || string.IsNullOrWhiteSpace(request.HtmlTemplate))
            throw new DomainException("Email subject and HTML body are required.");
        row.SubjectTemplate = request.SubjectTemplate.Trim(); row.HtmlTemplate = request.HtmlTemplate.Trim();
        row.TextTemplate = NullIfBlank(request.TextTemplate); row.IsEnabled = request.IsEnabled;
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task SendTestAsync(SendTestEmailRequest request, CancellationToken ct)
    {
        var config = await configurations.FirstOrDefaultAsync(x => true, ct) ?? throw new DomainException("Save email configuration first.");
        var currentUser = user.UserId.HasValue ? await users.GetByIdAsync(user.UserId.Value, ct) : null;
        var recipient = NullIfBlank(request.RecipientEmail) ?? currentUser?.Email ?? throw new DomainException("A test recipient is required.");
        try { _ = new MailAddress(recipient); } catch (FormatException) { throw new DomainException("Test recipient email is invalid."); }
        var company = await tenants.GetByIdAsync(TenantId, ct);
        try
        {
            await transport.SendAsync(ToDeliverySettings(config, protector), new RenderedEmail(recipient, currentUser?.DisplayName,
                $"{company?.Name ?? "HRMS"} email test", "<h2>Email is configured</h2><p>Your HRMS SMTP settings are working.</p>",
                "Email is configured. Your HRMS SMTP settings are working."), ct);
        }
        catch (CryptographicException)
        {
            throw new DomainException("The saved SMTP password can no longer be decrypted. Enter the SMTP key again and save the settings.");
        }
    }

    public static EmailDeliverySettings ToDeliverySettings(EmailConfiguration row, IEmailSecretProtector protector) =>
        new(row.Host, row.Port, row.Username,
            string.IsNullOrWhiteSpace(row.EncryptedPassword) ? null : protector.Unprotect(row.EncryptedPassword),
            row.UseTls, row.FromEmail, row.FromName, row.ReplyToEmail);

    private static void Validate(string host, int port, string fromEmail, string? replyTo, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new DomainException("SMTP host is required.");
        if (port is < 1 or > 65535) throw new DomainException("SMTP port must be between 1 and 65535.");
        try { _ = new MailAddress(fromEmail); } catch (Exception e) when (e is FormatException or ArgumentException) { throw new DomainException("From email is invalid."); }
        if (!string.IsNullOrWhiteSpace(replyTo)) try { _ = new MailAddress(replyTo); } catch (FormatException) { throw new DomainException("Reply-to email is invalid."); }
        if (!string.IsNullOrWhiteSpace(baseUrl) && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new DomainException("Application URL must be an absolute HTTP or HTTPS URL.");
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task<bool> EnsureTemplatesAsync(CancellationToken ct)
    {
        var existing = (await templates.ListAsync(cancellationToken: ct)).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = EmailTemplateCatalog.CreateDefaults(TenantId).Where(x => !existing.Contains(x.Key)).ToArray();
        foreach (var template in missing) await templates.AddAsync(template, ct);
        return missing.Length > 0;
    }
    private static EmailConfigurationDto Map(EmailConfiguration? x) => x is null
        ? new(false, "", 587, null, false, true, "", "", null, null, 0)
        : new(x.IsEnabled, x.Host, x.Port, x.Username, !string.IsNullOrWhiteSpace(x.EncryptedPassword), x.UseTls,
            x.FromEmail, x.FromName, x.ReplyToEmail, x.ApplicationBaseUrl, x.Version);
    private static EmailTemplateDto Map(EmailTemplate x) => new(x.Id, x.Key, x.Name, x.SubjectTemplate,
        x.HtmlTemplate, x.TextTemplate, x.IsEnabled, x.IsSystem, x.Version);
}
