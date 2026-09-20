using System.Net;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text;
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
using Microsoft.Extensions.Configuration;
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
    private DateTimeOffset _nextLinkCleanupAt = DateTimeOffset.MinValue;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
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
        var baseDomain = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Tenancy:BaseDomain"];
        var now = DateTimeOffset.UtcNow;
        if (now >= _nextLinkCleanupAt)
        {
            await db.EmailSignInLinks.IgnoreQueryFilters()
                .Where(x => x.ExpiresAt < now.AddDays(-7)).ExecuteDeleteAsync(ct);
            _nextLinkCleanupAt = now.AddDays(1);
        }
        var ids = await db.EmailOutboxItems.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.SentAt == null && x.AttemptCount < 8 && x.NextAttemptAt <= now)
            .OrderBy(x => x.TemplateKey == EmailTemplateKeys.AccountActivation || x.TemplateKey == EmailTemplateKeys.AccountCreated
                || x.TemplateKey == EmailTemplateKeys.PasswordReset ? 0 : 1)
            .ThenBy(x => x.NextAttemptAt).Select(x => x.Id).Take(5).ToListAsync(ct);

        foreach (var id in ids)
        {
            var item = await db.EmailOutboxItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null || item.SentAt is not null || item.NextAttemptAt > DateTimeOffset.UtcNow) continue;
            tenant.Set(item.TenantId);
            item.AttemptCount++;
            item.Version++;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            item.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); tenant.Clear(); continue; }

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
                var tenantBaseUrl = ResolveTenantBaseUrl(
                    configuration.ApplicationBaseUrl,
                    model.GetValueOrDefault("applicationBaseUrl"),
                    baseDomain,
                    company.Slug);
                model["applicationUrl"] = tenantBaseUrl;
                var relativeLink = model.GetValueOrDefault("link") ?? string.Empty;
                model["tenantSlug"] = company.Slug;
                var isAccountActivation = IsAccountActivation(item.TemplateKey, relativeLink);
                var queuedActivationUrl = model.GetValueOrDefault("actionUrl");
                model["actionUrl"] = SelectActionUrl(tenantBaseUrl, relativeLink, item.TemplateKey, queuedActivationUrl, company.Slug);
                if (!isAccountActivation && !string.IsNullOrWhiteSpace(tenantBaseUrl))
                {
                    var recipient = await db.Users.FirstOrDefaultAsync(x => x.Email == item.ToEmail && x.IsActive, ct);
                    if (recipient is not null)
                    {
                        var destination = SafeDestination(relativeLink, "/dashboard");
                        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                        db.EmailSignInLinks.Add(new EmailSignInLink
                        {
                            TenantId = item.TenantId, UserId = recipient.Id, RecipientEmail = recipient.Email,
                            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
                            Destination = destination,
                            ExpiresAt = EmailTemplateKeys.RequiresExpiringActionLink(item.TemplateKey)
                                ? DateTimeOffset.UtcNow.AddHours(24) : null
                        });
                        await db.SaveChangesAsync(ct);
                        model["actionUrl"] = $"{tenantBaseUrl}/email-link?token={token}";
                    }
                }
                var rendered = Render(item, template, model);
                var sendStarted = Stopwatch.GetTimestamp();
                await transport.SendAsync(EmailAdministrationService.ToDeliverySettings(configuration, protector), rendered, ct);
                logger.LogInformation("Email {EmailId} accepted by SMTP; queued for {QueueSeconds:F1}s, transport took {TransportMilliseconds:F0}ms.",
                    item.Id, (DateTimeOffset.UtcNow - item.CreatedAt).TotalSeconds, Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds);
                item.SentAt = DateTimeOffset.UtcNow; item.LastError = null;
                if (item.TemplateKey is EmailTemplateKeys.AccountCreated or EmailTemplateKeys.AccountActivation or EmailTemplateKeys.PasswordReset) item.ModelJson = "{}";
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                item.NextAttemptAt = DateTimeOffset.UtcNow.Add(item.AttemptCount switch
                {
                    1 => TimeSpan.FromSeconds(20),
                    2 => TimeSpan.FromMinutes(1),
                    3 => TimeSpan.FromMinutes(3),
                    4 => TimeSpan.FromMinutes(10),
                    5 => TimeSpan.FromMinutes(20),
                    _ => TimeSpan.FromMinutes(60)
                });
                if (item.AttemptCount >= 8)
                    logger.LogError(ex, "Email {EmailId} stopped after {Attempt} delivery attempts.", item.Id, item.AttemptCount);
                else
                    logger.LogWarning(ex, "Email {EmailId} delivery attempt {Attempt} failed; retry at {NextAttemptAt}.",
                        item.Id, item.AttemptCount, item.NextAttemptAt);
            }
            finally { await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); tenant.Clear(); }
        }
    }

    public static RenderedEmail Render(EmailOutboxItem item, EmailTemplate template, IReadOnlyDictionary<string, string?> model)
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

    public static string BuildActionUrl(string? baseUrl, string relativeLink, string? templateKey = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        var destination = string.IsNullOrWhiteSpace(relativeLink) ? "/" : relativeLink;
        if (Uri.TryCreate(destination, UriKind.Absolute, out var absolute)) destination = absolute.PathAndQuery;
        if (!destination.StartsWith('/')) destination = "/" + destination;
        if (IsAccountActivation(templateKey, destination)) return $"{baseUrl.TrimEnd('/')}{destination}";
        return $"{baseUrl.TrimEnd('/')}/login?returnUrl={Uri.EscapeDataString(destination)}";
    }

    private static bool IsAccountActivation(string? templateKey, string destination) =>
        templateKey == EmailTemplateKeys.AccountActivation ||
        destination.StartsWith("/activate?token=", StringComparison.OrdinalIgnoreCase);

    public static string SelectActionUrl(string tenantBaseUrl, string relativeLink, string? templateKey,
        string? queuedActionUrl, string tenantSlug) =>
        IsAccountActivation(templateKey, relativeLink) && IsValidActivationUrl(queuedActionUrl, tenantSlug)
            ? queuedActionUrl!
            : BuildActionUrl(tenantBaseUrl, relativeLink, templateKey);

    private static bool IsValidActivationUrl(string? value, string tenantSlug) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
        uri.AbsolutePath.Equals("/activate", StringComparison.OrdinalIgnoreCase) &&
        uri.Query.StartsWith("?token=", StringComparison.OrdinalIgnoreCase) &&
        (tenantSlug == "platform" || uri.Host.StartsWith(tenantSlug + ".", StringComparison.OrdinalIgnoreCase));

    public static string ResolveTenantBaseUrl(string? configuredUrl, string? requestedUrl, string? baseDomain, string slug) =>
        TenantDomains.BaseUrlForTenant(
            string.IsNullOrWhiteSpace(requestedUrl) ? configuredUrl : requestedUrl,
            baseDomain,
            slug);

    public static string SafeDestination(string? requested, string fallback)
    {
        if (string.IsNullOrWhiteSpace(requested) || !requested.StartsWith('/') || requested.StartsWith("//") ||
            requested.Contains('\\') || requested.Contains('\r') || requested.Contains('\n')) return fallback;
        return requested;
    }
}
