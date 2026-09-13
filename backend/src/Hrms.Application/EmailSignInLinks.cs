namespace Hrms.Application;

public sealed record ConsumedEmailSignInLink(Guid TenantId, Guid UserId, string RecipientEmail, string Destination);

public interface IEmailSignInLinkStore
{
    Task<Guid?> FindTenantIdAsync(string token, CancellationToken ct);
    Task<ConsumedEmailSignInLink?> ConsumeAsync(string token, Guid expectedTenantId, CancellationToken ct);
}
