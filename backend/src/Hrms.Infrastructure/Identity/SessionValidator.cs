using System.Security.Claims;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure.Identity;

public sealed class SessionValidator(HrmsDbContext db)
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
        if (tenant is null || tenant.Status is not (TenantStatus.Active or TenantStatus.Trial) || (tenant.Status == TenantStatus.Trial && tenant.TrialEndsAt <= now)) return false;
        if (tenant.Slug != "platform" && !await db.TenantSubscriptions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive && x.StartsAt <= now && (!x.EndsAt.HasValue || x.EndsAt > now), ct)) return false;
        if (!await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(x => x.Id == sessionId && x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, ct)) return false;
        var employee = await db.Employees.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted, ct);
        if (employee?.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned) return false;
        var roles = await (from link in db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            join role in db.Roles.IgnoreQueryFilters().AsNoTracking() on link.RoleId equals role.Id
            where link.UserId == userId && link.TenantId == tenantId && role.TenantId == tenantId && !link.IsDeleted && !role.IsDeleted
            select role).ToListAsync(ct);
        if (principal.Identity is not ClaimsIdentity identity) return false;
        foreach (var claim in identity.Claims.Where(x => x.Type is "permission" or "employee_id" or "platform_admin" || x.Type == ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new("platform_admin", user.IsPlatformAdmin ? "true" : "false"));
        if (employee is not null) identity.AddClaim(new("employee_id", employee.Id.ToString()));
        foreach (var permission in roles.SelectMany(x => x.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct()) identity.AddClaim(new("permission", permission));
        foreach (var role in roles) identity.AddClaim(new(ClaimTypes.Role, role.NormalizedName));
        return true;
    }
}
