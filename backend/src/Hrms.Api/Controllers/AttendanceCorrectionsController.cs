using Hrms.Application;
using Hrms.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/attendance-corrections"), Authorize]
public sealed class AttendanceCorrectionsController(AttendanceCorrectionService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AttendanceCorrectionDto>> Search([FromQuery] string scope = "mine", [FromQuery] WorkflowStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize), scope, status, ct);
    [HttpPost]
    public Task<AttendanceCorrectionDto> Submit(RequestAttendanceCorrection request, CancellationToken ct) => service.SubmitAsync(request, ct);
    [HttpPut("{id:guid}/review")]
    public Task<AttendanceCorrectionDto> Review(Guid id, ReviewAttendanceCorrection request, CancellationToken ct) => service.ReviewAsync(id, request, ct);
    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] long version, CancellationToken ct) { await service.CancelAsync(id, version, ct); return NoContent(); }
}
