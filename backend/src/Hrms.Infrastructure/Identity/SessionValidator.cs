using System.Security.Claims;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Hrms.Infrastructure.Identity;

public sealed class SessionValidator(HrmsDbContext db, Microsoft.AspNetCore.Http.IHttpContextAccessor? accessor = null, IConfiguration? configuration = null)
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(principal.FindFirstValue("session_id"), out var sessionId)) return false;
        // Authentication precedes tenant middleware. Every bypass is explicitly scoped
        // to the signed tenant and user; soft deletion still applies.
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && !x.IsDeleted && x.IsActive, ct);
        if (user is null) return false;
        var now = DateTimeOffset.UtcNow;
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted, ct);
        if (tenant is null || tenant.Status is not (TenantStatus.Active or TenantStatus.Trial)) return false;
        var razorpayProvider = string.Equals(configuration?["Billing:Razorpay:Mode"], "test", StringComparison.OrdinalIgnoreCase) ? "razorpay_test" : "razorpay_live";
        var cashfreeProvider = string.Equals(configuration?["Billing:Cashfree:Mode"], "test", StringComparison.OrdinalIgnoreCase) ? "cashfree_test" : "cashfree_live";
        var paid = await db.TenantSubscriptions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive && x.PlanCode != "trial"
            && (x.BillingProvider == null || x.BillingProvider == razorpayProvider || x.BillingProvider == cashfreeProvider)
            && x.StartsAt <= now && (!x.EndsAt.HasValue || x.EndsAt > now), ct);
        var authorized = await db.BillingCheckouts.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted
            && (x.Provider == razorpayProvider || x.Provider == cashfreeProvider)
            && (x.Status == "authenticated" || x.Status == "active" || x.Status == "payment_pending"), ct);
        var billingOnly = tenant.Slug != "platform" && !paid &&
            (tenant.Status != TenantStatus.Trial || tenant.TrialEndsAt <= now || (tenant.RequiresBillingMandate && !authorized));
        if (!await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(x => x.Id == sessionId && x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, ct)) return false;
        var employee = await db.Employees.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted, ct);
        if (employee?.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned) return false;
        var roles = await (from link in db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            join role in db.Roles.IgnoreQueryFilters().AsNoTracking() on link.RoleId equals role.Id
            where link.UserId == userId && link.TenantId == tenantId && role.TenantId == tenantId && !link.IsDeleted && !role.IsDeleted
            select role).ToListAsync(ct);
        if (billingOnly && (accessor?.HttpContext?.Request.Path.StartsWithSegments("/api/v1/billing") != true
            || !roles.Any(x => x.NormalizedName == "TENANT_ADMIN"))) return false;
        if (principal.Identity is not ClaimsIdentity identity) return false;
        foreach (var claim in identity.Claims.Where(x => x.Type is "permission" or "employee_id" or "platform_admin" || x.Type == ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new("platform_admin", user.IsPlatformAdmin ? "true" : "false"));
        if (employee is not null) identity.AddClaim(new("employee_id", employee.Id.ToString()));
        foreach (var permission in roles.SelectMany(x => x.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct()) identity.AddClaim(new("permission", permission));
        foreach (var role in roles) identity.AddClaim(new(ClaimTypes.Role, role.NormalizedName));
        return true;
    }
}
