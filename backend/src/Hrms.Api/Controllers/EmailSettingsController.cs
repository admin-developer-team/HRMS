using Hrms.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/email-settings"), Authorize(Policy = "PlatformAdmin")]
public sealed class EmailSettingsController(IEmailAdministrationService service) : ControllerBase
{
    [HttpGet]
    public Task<EmailConfigurationDto> Get(CancellationToken ct) => service.GetConfigurationAsync(ct);

    [HttpPut]
    public Task<EmailConfigurationDto> Update(UpdateEmailConfigurationRequest request, CancellationToken ct) =>
        service.UpdateConfigurationAsync(request, ct);

    [HttpPost("test")]
    public async Task<IActionResult> Test(SendTestEmailRequest request, CancellationToken ct)
    {
        await service.SendTestAsync(request, ct);
        return NoContent();
    }

    [HttpGet("templates")]
    public Task<IReadOnlyList<EmailTemplateDto>> Templates(CancellationToken ct) => service.ListTemplatesAsync(ct);

    [HttpPut("templates/{id:guid}")]
    public Task<EmailTemplateDto> UpdateTemplate(Guid id, UpdateEmailTemplateRequest request, CancellationToken ct) =>
        service.UpdateTemplateAsync(id, request, ct);
}
