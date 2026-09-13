namespace Hrms.Application;

public sealed record ConsumedEmailSignInLink(Guid TenantId, Guid UserId, string RecipientEmail, string Destination);

public interface IEmailSignInLinkStore
{
    Task<ConsumedEmailSignInLink?> ConsumeAsync(string token, CancellationToken ct);
}
