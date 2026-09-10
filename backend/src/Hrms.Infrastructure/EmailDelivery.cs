using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Encodings.Web;
using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Persistence;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Hrms.Infrastructure;

public sealed class DataProtectionEmailSecretProtector(IDataProtectionProvider provider) : IEmailSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("PeopleFlow.Email.SmtpPassword.v1");
    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}

public sealed class SmtpEmailTransport : IEmailTransport
{
    public async Task SendAsync(EmailDeliverySettings settings, RenderedEmail email, CancellationToken ct)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
            message.To.Add(new MailboxAddress(email.ToName ?? string.Empty, email.ToEmail));
            if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail)) message.ReplyTo.Add(MailboxAddress.Parse(settings.ReplyToEmail));
            message.Subject = email.Subject.Replace("\r", " ").Replace("\n", " ");
            message.Body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = email.TextBody }.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 30_000;
            var socket = settings.UseTls ? (settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls) : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Host, settings.Port, socket, ct);
            if (!string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (MailKit.Security.AuthenticationException ex) { throw new DomainException($"SMTP authentication failed. Verify the SMTP login and key. {ex.Message}"); }
        catch (System.Security.Authentication.AuthenticationException ex) { throw new DomainException($"SMTP TLS authentication failed. Verify the server and TLS setting. {ex.Message}"); }
        catch (SmtpCommandException ex) { throw new DomainException($"SMTP server rejected the message ({ex.StatusCode}). {ex.Message}"); }
        catch (SmtpProtocolException ex) { throw new DomainException($"SMTP protocol error. Verify the port and TLS setting. {ex.Message}"); }
        catch (SocketException ex) { throw new DomainException($"Could not connect to the SMTP server. {ex.Message}"); }
    }
}

public sealed class GlobalEmailConfigurationReader(HrmsDbContext db) : IGlobalEmailConfigurationReader
{
    public Task<bool> IsEnabledAsync(CancellationToken ct) => db.EmailConfigurations.IgnoreQueryFilters()
        .AnyAsync(x => x.TenantId == DatabaseInitializer.PlatformTenantId && !x.IsDeleted && x.IsEnabled, ct);
}

public sealed class EmailOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Email outbox processing failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var protector = scope.ServiceProvider.GetRequiredService<IEmailSecretProtector>();
        var transport = scope.ServiceProvider.GetRequiredService<IEmailTransport>();
        var now = DateTimeOffset.UtcNow;
        var ids = await db.EmailOutboxItems.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.SentAt == null && x.AttemptCount < 8 && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Select(x => x.Id).Take(20).ToListAsync(ct);

        foreach (var id in ids)
        {
            var item = await db.EmailOutboxItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null || item.SentAt is not null || item.NextAttemptAt > DateTimeOffset.UtcNow) continue;
            tenant.Set(item.TenantId);
            item.AttemptCount++;
            item.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); continue; }

            try
            {
                var configuration = await db.EmailConfigurations.IgnoreQueryFilters().FirstOrDefaultAsync(
                    x => x.TenantId == DatabaseInitializer.PlatformTenantId && !x.IsDeleted && x.IsEnabled, ct);
                if (configuration is null) { item.LastError = "Email delivery is disabled or not configured."; continue; }
                var template = await db.EmailTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(
                    x => x.TenantId == DatabaseInitializer.PlatformTenantId && !x.IsDeleted && x.Key == item.TemplateKey && x.IsEnabled, ct)
                    ?? await db.EmailTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(
                        x => x.TenantId == DatabaseInitializer.PlatformTenantId && !x.IsDeleted && x.Key == EmailTemplateKeys.DefaultNotification && x.IsEnabled, ct)
                    ?? throw new InvalidOperationException($"No enabled email template exists for '{item.TemplateKey}'.");
                var company = await db.Tenants.IgnoreQueryFilters().FirstAsync(x => x.Id == item.TenantId, ct);
                var model = JsonSerializer.Deserialize<Dictionary<string, string?>>(item.ModelJson) ?? [];
                model["companyName"] = company.Name;
                model["recipientName"] = item.ToName ?? "there";
                model["applicationUrl"] = configuration.ApplicationBaseUrl ?? string.Empty;
                var relativeLink = model.GetValueOrDefault("link") ?? string.Empty;
                model["tenantSlug"] = company.Slug;
                model["actionUrl"] = BuildActionUrl(configuration.ApplicationBaseUrl, company.Slug, relativeLink);
                var rendered = Render(item, template, model);
                await transport.SendAsync(EmailAdministrationService.ToDeliverySettings(configuration, protector), rendered, ct);
                item.SentAt = DateTimeOffset.UtcNow; item.LastError = null;
                if (item.TemplateKey is EmailTemplateKeys.AccountCreated or EmailTemplateKeys.PasswordReset) item.ModelJson = "{}";
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                item.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, item.AttemptCount)));
                logger.LogWarning(ex, "Email {EmailId} delivery attempt {Attempt} failed.", item.Id, item.AttemptCount);
            }
            finally { await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); tenant.Clear(); }
        }
    }

    private static RenderedEmail Render(EmailOutboxItem item, EmailTemplate template, IReadOnlyDictionary<string, string?> model)
    {
        var subject = Replace(template.SubjectTemplate, model, false);
        var html = Replace(template.HtmlTemplate, model, true);
        var text = Replace(template.TextTemplate ?? "{{title}}\n\n{{message}}\n\n{{actionUrl}}", model, false);
        return new(item.ToEmail, item.ToName, subject, html, text);
    }

    private static string Replace(string template, IReadOnlyDictionary<string, string?> model, bool htmlEncode)
    {
        var result = template;
        foreach (var pair in model)
        {
            var value = pair.Value ?? string.Empty;
            result = result.Replace("{{" + pair.Key + "}}", htmlEncode ? HtmlEncoder.Default.Encode(value) : value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    internal static string BuildActionUrl(string? baseUrl, string tenantSlug, string relativeLink)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        var destination = string.IsNullOrWhiteSpace(relativeLink) ? "/" : relativeLink;
        if (Uri.TryCreate(destination, UriKind.Absolute, out var absolute)) destination = absolute.PathAndQuery;
        if (!destination.StartsWith('/')) destination = "/" + destination;
        var query = $"tenant={Uri.EscapeDataString(tenantSlug)}&returnUrl={Uri.EscapeDataString(destination)}";
        return $"{baseUrl.TrimEnd('/')}/login?{query}";
    }
}
