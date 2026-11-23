using System.Text.Json;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class PasswordRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Only_current_workspace_code_changes_password_once(bool platform)
    {
        var tenant = new CurrentTenant();
        tenant.Set(Guid.NewGuid());
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant, new TestCurrentUser(), new TestNotificationPublisher());
        var user = new UserAccount { TenantId = tenant.TenantId!.Value, Email = "user@example.test", IsPlatformAdmin = platform };
        db.Users.Add(user);
        var refresh = new RefreshToken { TenantId = user.TenantId, UserId = user.Id };
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync();
        var protector = new DataProtectionEmailSecretProtector(new EphemeralDataProtectionProvider());
        var hasher = new Pbkdf2PasswordHasher();
        var service = new PasswordRecoveryService(db, hasher, protector);
        await service.RequestAsync("missing@example.test", default);
        Assert.Empty(await db.EmailOutboxItems.ToListAsync());
        await service.RequestAsync(user.Email, default);
        await service.RequestAsync(user.Email, default);
        var item = await db.EmailOutboxItems.SingleAsync();
        var code = protector.Unprotect(JsonDocument.Parse(item.ModelJson).RootElement.GetProperty("ProtectedCode").GetString()!);
        Assert.Matches("^[0-9]{8}$", code);
        tenant.Set(Guid.NewGuid());
        await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(user.Email, code, "new-password", default));
        tenant.Set(user.TenantId);
        await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(user.Email, "invalid!", "new-password", default));
        Assert.Equal(1, user.PasswordResetAttempts);
        for (var attempt = 1; attempt < 5; attempt++)
            await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(user.Email, "invalid!", "new-password", default));
        await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(user.Email, code, "new-password", default));

        user.PasswordResetExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(user.Email, code, "new-password", default));
        await service.RequestAsync(user.Email, default);
        var replacement = await db.EmailOutboxItems.SingleAsync(x => x.Id != item.Id);
        var replacementCode = protector.Unprotect(JsonDocument.Parse(replacement.ModelJson).RootElement.GetProperty("ProtectedCode").GetString()!);
        await service.ConfirmAsync(user.Email, replacementCode, "new-password", default);
        Assert.True(hasher.Verify("new-password", user.PasswordHash));
        Assert.NotNull(refresh.RevokedAt);
        await Assert.ThrowsAsync<DomainException>(() => service.ConfirmAsync(user.Email, replacementCode, "next-password", default));
    }
}

