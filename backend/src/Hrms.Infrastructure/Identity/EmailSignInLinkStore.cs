using System.Security.Cryptography;
using System.Text;
using Hrms.Application;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure.Identity;

public sealed class EmailSignInLinkStore(HrmsDbContext db) : IEmailSignInLinkStore
{
    public Task<Guid?> FindTenantIdAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256) return Task.FromResult<Guid?>(null);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTimeOffset.UtcNow;
        return db.EmailSignInLinks.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TokenHash == hash && !x.IsDeleted && (x.ExpiresAt == null || (x.ExpiresAt > now && x.UsedAt == null)))
            .Select(x => (Guid?)x.TenantId).SingleOrDefaultAsync(ct);
    }

    public async Task<ConsumedEmailSignInLink?> ConsumeAsync(string token, Guid expectedTenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTimeOffset.UtcNow;
        var reusable = await db.EmailSignInLinks.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TokenHash == hash && x.TenantId == expectedTenantId && !x.IsDeleted && x.ExpiresAt == null)
            .Select(x => new ConsumedEmailSignInLink(x.TenantId, x.UserId, x.RecipientEmail, x.Destination))
            .SingleOrDefaultAsync(ct);
        if (reusable is not null) return reusable;
        if (!await db.EmailSignInLinks.IgnoreQueryFilters().AsNoTracking().AnyAsync(
            x => x.TokenHash == hash && x.TenantId == expectedTenantId && !x.IsDeleted
                && x.UsedAt == null && x.ExpiresAt > now, ct)) return null;
        var changed = await db.EmailSignInLinks.IgnoreQueryFilters()
            .Where(x => x.TokenHash == hash && x.TenantId == expectedTenantId && !x.IsDeleted && x.UsedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.UsedAt, now), ct);
        if (changed != 1) return null;
        var link = await db.EmailSignInLinks.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.TokenHash == hash && x.TenantId == expectedTenantId, ct);
        return new(link.TenantId, link.UserId, link.RecipientEmail, link.Destination);
    }
}
