using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure;

public sealed record PublicTrialRequest(string CompanyName, string Slug, string AdminName, string AdminEmail,
    string? PreferredPlanCode, string? ApplicationBaseUrl = null);
public sealed record ActivateAccountRequest(string Token, string Password, string? LegalName, string? TimeZone);
public sealed record SupportTicketRequest(string ContactName, string ContactEmail, string Category, string Subject, string Description);
public sealed record SupportTicketDto(Guid Id, string Reference, string ContactName, string ContactEmail, string Category, string Subject,
    string Description, string Status, Guid? SourceTenantId, Guid? AssignedToUserId, string? InternalNote, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, long Version);
public sealed record SupportTicketUpdate(string Status, Guid? AssignedToUserId, string? InternalNote, long Version);
public sealed record SupportTeamMember(Guid Id, string Name, string Email, bool InboxEnabled, bool Active, bool CanRead, bool CanManage);
public sealed record SupportAccessUpdate(bool Enabled);
public sealed record InviteSupportUserRequest(string Name, string Email);

public sealed class PlatformExperienceService(HrmsDbContext db, ICurrentTenant currentTenant, ICurrentUser actor, IPasswordHasher hasher)
{
    private static readonly Guid PlatformId = DatabaseInitializer.PlatformTenantId;
    private static readonly string[] ReservedSlugs = ["platform", "www", "api", "app", "admin", "mail", "support", "billing", "status", "login", "signup", "help", "static"];

    public Task RequestTrialAsync(PublicTrialRequest request, CancellationToken ct) =>
        RequestTrialAsync(request, null, ct);

    public async Task RequestTrialAsync(PublicTrialRequest request, string? applicationBaseUrl, CancellationToken ct)
    {
        if (currentTenant.TenantId != PlatformId) throw new UnauthorizedAccessException("Start a trial on the platform website.");
        var slug = request.Slug.Trim().ToLowerInvariant();
        var name = request.CompanyName.Trim();
        var adminName = request.AdminName.Trim();
        var email = ValidateEmail(request.AdminEmail);
        if (!TenantDomains.IsValidSlug(slug) || ReservedSlugs.Contains(slug)) throw new DomainException("Choose a different company workspace address.");
        if (name.Length is < 2 or > 150 || adminName.Length is < 2 or > 120) throw new DomainException("Enter a valid company and administrator name.");
        if (await db.Tenants.AnyAsync(x => x.Slug == slug, ct)) throw new DomainException("This workspace address is already in use.");
        var plan = (request.PreferredPlanCode ?? "starter").Trim().ToLowerInvariant();
        if (plan is not ("starter" or "professional" or "enterprise")) throw new DomainException("Choose a supported plan.");
        var tenant = new Tenant { Name = name, Slug = slug, Status = TenantStatus.Trial, TrialEndsAt = null,
            PreferredPlanCode = plan, DefaultCurrency = "INR", TimeZone = "Asia/Kolkata" };
        db.Tenants.Add(tenant);
        currentTenant.Set(tenant.Id, slug);
        try
        {
            var adminRole = new Role { TenantId = tenant.Id, Name = "Tenant Administrator", NormalizedName = "TENANT_ADMIN", PermissionsCsv = "*", IsSystem = true };
            var admin = new UserAccount { TenantId = tenant.Id, Email = email, DisplayName = adminName,
                PasswordHash = hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))), IsActive = false };
            db.Roles.Add(adminRole);
            foreach (var definition in Permissions.TenantSystemRoles)
                db.Roles.Add(new Role { TenantId = tenant.Id, Name = definition.Name, NormalizedName = definition.NormalizedName,
                    PermissionsCsv = string.Join(',', definition.Permissions), IsSystem = true });
            db.Users.Add(admin);
            db.UserRoles.Add(new UserRole { TenantId = tenant.Id, UserId = admin.Id, RoleId = adminRole.Id });
            db.TenantSubscriptions.Add(new TenantSubscription { TenantId = tenant.Id, PlanCode = "trial", EmployeeLimit = 50,
                StartsAt = DateTimeOffset.UtcNow, IsActive = false });
            db.LeaveTypes.Add(new LeaveType { TenantId = tenant.Id, Name = "Annual Leave", Code = "ANNUAL", AnnualAllowance = 20, IsPaid = true });
            db.LeaveTypes.Add(new LeaveType { TenantId = tenant.Id, Name = "Sick Leave", Code = "SICK", AnnualAllowance = 10, IsPaid = true });
            await QueueActivationAsync(admin, "trial", applicationBaseUrl, ct);
            await db.SaveChangesAsync(ct);
        }
        finally { currentTenant.Set(PlatformId, "platform"); }
    }

    public Task ResendTrialAsync(string slug, string email, CancellationToken ct) =>
        ResendTrialAsync(slug, email, null, ct);

    public async Task ResendTrialAsync(string slug, string email, string? applicationBaseUrl, CancellationToken ct)
    {
        if (currentTenant.TenantId != PlatformId) throw new UnauthorizedAccessException();
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == slug.Trim().ToLower() && x.Status == TenantStatus.Trial && x.TrialEndsAt == null && !x.IsDeleted, ct);
        if (tenant is null) return;
        currentTenant.Set(tenant.Id, tenant.Slug);
        try
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower() && !x.IsActive, ct);
            if (user is null) return;
            foreach (var old in await db.AccountActivations.Where(x => x.UserId == user.Id && x.UsedAt == null).ToListAsync(ct)) old.UsedAt = DateTimeOffset.UtcNow;
            await QueueActivationAsync(user, "trial", applicationBaseUrl, ct);
            await db.SaveChangesAsync(ct);
        }
        finally { currentTenant.Set(PlatformId, "platform"); }
    }

    public async Task ActivateAsync(ActivateAccountRequest request, CancellationToken ct)
    {
        if (request.Password.Length is < 12 or > 256) throw new DomainException("Use a password of at least 12 characters.");
        if (string.IsNullOrWhiteSpace(request.Token)) throw new DomainException("The activation link is invalid or expired.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var invite = await db.AccountActivations.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsDeleted, ct);
        if (invite is null || invite.UsedAt.HasValue || invite.ExpiresAt <= DateTimeOffset.UtcNow || invite.TenantId != currentTenant.TenantId)
            throw new DomainException("The activation link is invalid or expired.");
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == invite.UserId && x.TenantId == invite.TenantId && !x.IsDeleted, ct)
            ?? throw new DomainException("The activation link is invalid or expired.");
        if (user.IsActive) throw new DomainException("This account is already active.");
        var tenant = await db.Tenants.FirstAsync(x => x.Id == invite.TenantId, ct);
        user.PasswordHash = hasher.Hash(request.Password);
        user.IsActive = true;
        invite.UsedAt = DateTimeOffset.UtcNow;
        if (invite.Kind == "trial")
        {
            if (tenant.TrialEndsAt.HasValue) throw new DomainException("This trial has already started.");
            var now = DateTimeOffset.UtcNow;
            tenant.TrialEndsAt = now.AddDays(30);
            if (!string.IsNullOrWhiteSpace(request.LegalName)) tenant.LegalName = request.LegalName.Trim()[..Math.Min(200, request.LegalName.Trim().Length)];
            if (!string.IsNullOrWhiteSpace(request.TimeZone))
            {
                try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone); tenant.TimeZone = request.TimeZone.Trim(); }
                catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new DomainException("Time zone is invalid."); }
            }
            var subscription = await db.TenantSubscriptions.SingleAsync(x => x.PlanCode == "trial", ct);
            subscription.StartsAt = now; subscription.EndsAt = tenant.TrialEndsAt; subscription.IsActive = true;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> CreateTicketAsync(SupportTicketRequest request, CancellationToken ct)
    {
        var name = request.ContactName.Trim(); var email = ValidateEmail(request.ContactEmail);
        var subject = request.Subject.Trim(); var description = request.Description.Trim();
        if (name.Length is < 2 or > 120 || subject.Length is < 5 or > 180 || description.Length is < 15 or > 10000)
            throw new DomainException("Please enter your name, a short subject, and a description of at least 15 characters.");
        var category = request.Category.Trim().ToLowerInvariant();
        if (category is not ("general" or "billing" or "account" or "technical" or "feedback")) throw new DomainException("Choose a ticket category.");
        var source = currentTenant.TenantId == PlatformId ? null : currentTenant.TenantId;
        currentTenant.Set(PlatformId, "platform");
        try
        {
            var ticket = new SupportTicket { TenantId = PlatformId, Reference = "PF-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                SourceTenantId = source, ContactName = name, ContactEmail = email, Category = category, Subject = subject, Description = description };
            db.SupportTickets.Add(ticket);
            QueueEmail(email, name, "We received your support request", $"Your reference is {ticket.Reference}. Our team will review your request.", null, "/help");
            foreach (var recipient in await SupportRecipientsAsync(ct))
            {
                db.UserNotifications.Add(new UserNotification { TenantId = PlatformId, UserId = recipient.Id,
                    Title = $"New support request {ticket.Reference}", Message = subject, Kind = "info", Link = "/support" });
                QueueEmail(recipient.Email, recipient.DisplayName, $"New support request {ticket.Reference}", $"{name} submitted: {subject}", null, "/support");
            }
            await db.SaveChangesAsync(ct);
            return ticket.Reference;
        }
        finally { currentTenant.Set(source ?? PlatformId); }
    }

    public async Task<IReadOnlyList<SupportTicketDto>> TicketsAsync(CancellationToken ct)
    {
        await RequireSupportAccessAsync(false, ct);
        return await db.SupportTickets.OrderByDescending(x => x.CreatedAt).Take(200).Select(x =>
            new SupportTicketDto(x.Id, x.Reference, x.ContactName, x.ContactEmail, x.Category, x.Subject, x.Description,
                x.Status, x.SourceTenantId, x.AssignedToUserId, x.InternalNote, x.CreatedAt, x.ResolvedAt, x.Version)).ToListAsync(ct);
    }

    public async Task<SupportTicketDto> UpdateTicketAsync(Guid id, SupportTicketUpdate request, CancellationToken ct)
    {
        await RequireSupportAccessAsync(true, ct);
        if (request.Status is not ("open" or "in_progress" or "waiting" or "resolved" or "closed")) throw new DomainException("Invalid ticket status.");
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Ticket not found.");
        if (ticket.Version != request.Version) throw new DomainException("This ticket changed. Refresh and try again.");
        if (request.InternalNote?.Length > 4000) throw new DomainException("Internal note is too long.");
        if (request.AssignedToUserId.HasValue)
        {
            var candidate = (await SupportRecipientsAsync(ct)).FirstOrDefault(x => x.Id == request.AssignedToUserId.Value);
            if (candidate is null) throw new DomainException("Choose a support team member with inbox access.");
            if (ticket.AssignedToUserId != candidate.Id)
            {
                db.UserNotifications.Add(new UserNotification { TenantId = PlatformId, UserId = candidate.Id,
                    Title = $"Ticket {ticket.Reference} assigned to you", Message = ticket.Subject, Kind = "info", Link = "/support" });
                QueueEmail(candidate.Email, candidate.DisplayName, $"Ticket {ticket.Reference} assigned to you", ticket.Subject, null, "/support");
            }
        }
        ticket.Status = request.Status; ticket.AssignedToUserId = request.AssignedToUserId;
        ticket.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote.Trim();
        ticket.ResolvedAt = request.Status is "resolved" or "closed" ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Map(ticket);
    }

    public async Task<IReadOnlyList<SupportTeamMember>> TeamAsync(CancellationToken ct)
    {
        var isManager = currentTenant.TenantId == PlatformId && actor.IsPlatformAdmin && actor.HasPermission(Permissions.PlatformManage);
        if (!isManager) await RequireSupportAccessAsync(false, ct);
        var users = await db.Users.OrderBy(x => x.DisplayName).ToListAsync(ct);
        var roles = await db.Roles.ToListAsync(ct);
        var links = await db.UserRoles.ToListAsync(ct);
        var members = users.Select(user =>
        {
            var permissions = roles.Where(x => links.Any(link => link.UserId == user.Id && link.RoleId == x.Id))
                .SelectMany(x => x.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToHashSet();
            return new SupportTeamMember(user.Id, user.DisplayName, user.Email, user.SupportInboxEnabled, user.IsActive,
                permissions.Contains("*") || permissions.Contains(Permissions.SupportRead),
                permissions.Contains("*") || permissions.Contains(Permissions.SupportManage));
        }).ToArray();
        return isManager ? members : members.Where(x => x.Active && x.InboxEnabled && x.CanRead).ToArray();
    }

    public async Task SetSupportAccessAsync(Guid id, bool enabled, CancellationToken ct)
    {
        RequirePlatformManager();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Platform user not found.");
        user.SupportInboxEnabled = enabled;
        await db.SaveChangesAsync(ct);
    }

    public async Task InviteSupportUserAsync(InviteSupportUserRequest request, CancellationToken ct)
    {
        RequirePlatformManager();
        var email = ValidateEmail(request.Email);
        var name = request.Name.Trim();
        if (name.Length is < 2 or > 120) throw new DomainException("Enter the support member's name.");
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw new DomainException("This platform email already has an account.");
        var role = await db.Roles.SingleAsync(x => x.NormalizedName == "SUPPORT_AGENT", ct);
        var user = new UserAccount { TenantId = PlatformId, DisplayName = name, Email = email,
            PasswordHash = hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))), IsActive = false, SupportInboxEnabled = true };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { TenantId = PlatformId, UserId = user.Id, RoleId = role.Id });
        await QueueActivationAsync(user, "support", null, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task QueueActivationAsync(UserAccount user, string kind, string? applicationBaseUrl, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var activationUrl = BuildActivationUrl(applicationBaseUrl, currentTenant.Slug, token);
        db.AccountActivations.Add(new AccountActivation { TenantId = user.TenantId, UserId = user.Id,
            Kind = kind, TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        QueueEmail(user.Email, user.DisplayName, kind == "trial" ? "Activate your 30-day PeopleFlow trial" : "Activate your PeopleFlow support account",
            kind == "trial" ? "Your company workspace is ready. Set your password to start the 30-day trial." : "Your support workspace is ready. Set your password to get started.",
            user.TenantId, $"/activate?token={token}", EmailTemplateKeys.AccountActivation, applicationBaseUrl, activationUrl);
        await Task.CompletedTask;
    }

    private void QueueEmail(string email, string? name, string title, string message, Guid? tenantId, string link,
        string templateKey = EmailTemplateKeys.DefaultNotification, string? applicationBaseUrl = null, string? actionUrl = null)
    {
        db.EmailOutboxItems.Add(new EmailOutboxItem { TenantId = tenantId ?? PlatformId, ToEmail = email, ToName = name,
            TemplateKey = templateKey,
            ModelJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["title"] = title,
                ["message"] = message,
                ["link"] = link,
                ["applicationBaseUrl"] = applicationBaseUrl,
                ["actionUrl"] = actionUrl
            }),
            NextAttemptAt = DateTimeOffset.UtcNow });
    }

    public static string? BuildActivationUrl(string? applicationBaseUrl, string? tenantSlug, string token)
    {
        if (string.IsNullOrWhiteSpace(applicationBaseUrl) || string.IsNullOrWhiteSpace(tenantSlug) ||
            string.IsNullOrWhiteSpace(token) || !Uri.TryCreate(applicationBaseUrl, UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("http" or "https")) return null;
        var builder = new UriBuilder(origin) { Path = "/activate", Query = $"token={Uri.EscapeDataString(token)}" };
        if (tenantSlug != "platform" && !builder.Host.StartsWith(tenantSlug + ".", StringComparison.OrdinalIgnoreCase))
            builder.Host = $"{tenantSlug}.{builder.Host}";
        return builder.Uri.AbsoluteUri;
    }

    private async Task<IReadOnlyList<UserAccount>> SupportRecipientsAsync(CancellationToken ct)
    {
        var users = await db.Users.Where(x => x.IsActive && x.SupportInboxEnabled).ToListAsync(ct);
        var roles = await db.Roles.ToListAsync(ct);
        var links = await db.UserRoles.ToListAsync(ct);
        return users.Where(user => roles.Where(x => links.Any(l => l.UserId == user.Id && l.RoleId == x.Id))
            .Any(role => role.PermissionsCsv.Split(',').Any(p => p is Permissions.All or Permissions.SupportRead))).ToArray();
    }

    private async Task RequireSupportAccessAsync(bool manage, CancellationToken ct)
    {
        if (currentTenant.TenantId != PlatformId || actor.UserId is null || !actor.HasPermission(manage ? Permissions.SupportManage : Permissions.SupportRead))
            throw new UnauthorizedAccessException();
        if (!await db.Users.AnyAsync(x => x.Id == actor.UserId && x.SupportInboxEnabled && x.IsActive, ct))
            throw new UnauthorizedAccessException("Support inbox access is disabled for this account.");
    }

    private void RequirePlatformManager()
    {
        if (currentTenant.TenantId != PlatformId || !actor.IsPlatformAdmin || !actor.HasPermission(Permissions.PlatformManage))
            throw new UnauthorizedAccessException();
    }

    private static string ValidateEmail(string value)
    {
        var email = value.Trim().ToLowerInvariant();
        if (email.Length > 254) throw new DomainException("Enter a valid email address.");
        try { if (new MailAddress(email).Address != email) throw new FormatException(); }
        catch (FormatException) { throw new DomainException("Enter a valid email address."); }
        return email;
    }

    private static SupportTicketDto Map(SupportTicket x) => new(x.Id, x.Reference, x.ContactName, x.ContactEmail, x.Category, x.Subject,
        x.Description, x.Status, x.SourceTenantId, x.AssignedToUserId, x.InternalNote, x.CreatedAt, x.ResolvedAt, x.Version);
}
