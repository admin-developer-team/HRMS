using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure;

internal sealed record RecoveryEmail(Guid UserId, DateTimeOffset ExpiresAt, string ProtectedCode);

public sealed class PasswordRecoveryService(HrmsDbContext db, IPasswordHasher hasher, IEmailSecretProtector protector)
{
    private const string InvalidCode = "The code is invalid or expired. Request a new code and try again.";

    public async Task RequestAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.IsActive && x.Email == normalized, ct);
        var now = DateTimeOffset.UtcNow;
        if (user is null || user.PasswordResetExpiresAt > now.AddMinutes(9)) return;

        var code = RandomNumberGenerator.GetInt32(100_000_000).ToString("D8", CultureInfo.InvariantCulture);
        // PostgreSQL timestamps have microsecond precision. Match the queued challenge exactly.
        var expires = DateTimeOffset.FromUnixTimeSeconds(now.AddMinutes(10).ToUnixTimeSeconds());
        user.PasswordResetHash = hasher.Hash(Challenge(user, code));
        user.PasswordResetAttempts = 0;
        user.PasswordResetExpiresAt = expires;
        db.EmailOutboxItems.Add(new EmailOutboxItem
        {
            TenantId = user.TenantId, ToEmail = user.Email, ToName = user.DisplayName,
            TemplateKey = EmailTemplateKeys.RecoveryCode, NextAttemptAt = now, CreatedAt = now, Version = 1,
            ModelJson = JsonSerializer.Serialize(new RecoveryEmail(user.Id, expires, protector.Protect(code)))
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* Another request updated the account. */ }
    }

    public async Task ConfirmAsync(string email, string code, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        // Serialize verification for each account so simultaneous guesses each consume an attempt.
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var accounts = db.Database.IsNpgsql()
            ? db.Users.FromSqlInterpolated($"""SELECT * FROM "Users" WHERE "TenantId" = {db.CurrentTenantId} AND "Email" = {normalized} FOR UPDATE""")
            : db.Users.AsQueryable();
        var user = await accounts.SingleOrDefaultAsync(x => x.IsActive && x.Email == normalized, ct);
        if (user?.PasswordResetHash is null || user.PasswordResetExpiresAt <= DateTimeOffset.UtcNow ||
            user.PasswordResetAttempts >= 5) throw new DomainException(InvalidCode);

        if (!hasher.Verify(Challenge(user, code), user.PasswordResetHash))
        {
            user.PasswordResetAttempts++;
            await PersistAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            throw new DomainException(InvalidCode);
        }

        user.PasswordHash = hasher.Hash(password);
        user.PasswordResetHash = null;
        user.PasswordResetExpiresAt = null;
        user.PasswordResetAttempts = 0;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        var now = DateTimeOffset.UtcNow;
        foreach (var token in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct))
            token.RevokedAt = now;
        foreach (var link in await db.EmailSignInLinks.Where(l => l.UserId == user.Id && l.UsedAt == null).ToListAsync(ct))
            link.UsedAt = now;
        await PersistAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private static string Challenge(UserAccount user, string code) => $"{user.Id}:{user.Email}:{user.PasswordHash}:{code}";

    private async Task PersistAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new DomainException(InvalidCode); }
    }
}

