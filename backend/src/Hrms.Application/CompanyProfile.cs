using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record CompanyProfileDto(Guid Id, string Name, string? LegalName, string Slug, string DefaultCurrency,
    string TimeZone, string Locale, Guid? LogoDocumentId, long Version);
public sealed record UpdateCompanyProfileRequest(string Name, string? LegalName, string DefaultCurrency, string TimeZone, string Locale, long Version);

public interface ICompanyProfileService
{
    Task<CompanyProfileDto> GetAsync(CancellationToken ct);
    Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileRequest request, CancellationToken ct);
}

public sealed class CompanyProfileService(
    IRepository<Tenant> tenants,
    IRepository<StoredDocument> documents,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICompanyProfileService
{
    private Guid TenantId => currentTenant.TenantId ?? throw new UnauthorizedAccessException("Tenant identity is missing.");

    public async Task<CompanyProfileDto> GetAsync(CancellationToken ct) =>
        await MapAsync(await tenants.GetByIdAsync(TenantId, ct) ?? throw new KeyNotFoundException("Company not found."), ct);

    public async Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileRequest request, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.IdentityManage)) throw new UnauthorizedAccessException("Company settings access is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new DomainException("Company name is required.");
        var row = await tenants.GetByIdAsync(TenantId, ct) ?? throw new KeyNotFoundException("Company not found.");
        if (row.Version != request.Version) throw new DomainException("The company profile changed. Reload and retry.");
        row.Name = request.Name.Trim();
        row.LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim();
        row.DefaultCurrency = request.DefaultCurrency.Trim().ToUpperInvariant();
        row.TimeZone = request.TimeZone.Trim();
        row.Locale = request.Locale.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return await MapAsync(row, ct);
    }

    private async Task<CompanyProfileDto> MapAsync(Tenant row, CancellationToken ct)
    {
        var logos = await documents.ListAsync(x => x.OwnerType == DocumentOwnerType.Tenant && x.OwnerId == row.Id && x.Category == "logo",
            x => x.OrderByDescending(d => d.CreatedAt), take: 1, cancellationToken: ct);
        return new(row.Id, row.Name, row.LegalName, row.Slug, row.DefaultCurrency, row.TimeZone, row.Locale, logos.FirstOrDefault()?.Id, row.Version);
    }
}
