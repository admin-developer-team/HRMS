using Hrms.Application;
using Hrms.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/documents"), Authorize]
public sealed class DocumentsController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DocumentDto>> List([FromQuery] DocumentOwnerType ownerType, [FromQuery] Guid ownerId,
        [FromQuery] string? category, CancellationToken ct) => service.ListAsync(ownerType, ownerId, category, ct);

    [HttpPost, RequestSizeLimit(DocumentService.MaxFileSize)]
    public async Task<DocumentDto> Upload([FromQuery] DocumentOwnerType ownerType, [FromQuery] Guid ownerId,
        [FromQuery] string category, [FromQuery] bool replace, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await service.UploadAsync(ownerType, ownerId, category, file.FileName, file.ContentType, file.Length, content, replace, ct);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var document = await service.OpenAsync(id, ct);
        return File(document.Stream, document.ContentType, document.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController, Route("api/v1/company-profile"), Authorize]
public sealed class CompanyProfileController(ICompanyProfileService service) : ControllerBase
{
    [HttpGet]
    public Task<CompanyProfileDto> Get(CancellationToken ct) => service.GetAsync(ct);

    [HttpPut, Authorize(Policy = Permissions.IdentityManage)]
    public Task<CompanyProfileDto> Update(UpdateCompanyProfileRequest request, CancellationToken ct) => service.UpdateAsync(request, ct);
}

[ApiController, Route("api/v1/notifications"), Authorize]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public Task<NotificationFeedDto> Feed([FromQuery] int take = 30, CancellationToken ct = default) => service.GetFeedAsync(take, ct);

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await service.MarkAllReadAsync(ct);
        return NoContent();
    }
}
