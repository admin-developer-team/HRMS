using Hrms.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/calendar"), Authorize]
public sealed class CalendarController(CalendarService service) : ControllerBase
{
    [HttpGet]
    public Task<CalendarMonth> Get([FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? locationId, CancellationToken ct) =>
        service.GetAsync(year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month, locationId, ct);

    [HttpPut("optional-holidays/{id:guid}/selection"), Authorize(Policy = "EmployeeLinked")]
    public async Task<IActionResult> Select(Guid id, SelectHolidayRequest request, CancellationToken ct)
    {
        await service.SelectAsync(id, request.Selected, ct);
        return NoContent();
    }
}

[ApiController, Route("api/v1/calendar/meetings"), Authorize]
public sealed class MeetingsController(MeetingService service) : ControllerBase
{
    [HttpGet("people")] public Task<IReadOnlyList<MeetingPerson>> People([FromQuery] string? search, CancellationToken ct) => service.CandidatesAsync(search, ct);
    [HttpPost] public Task<MeetingDto> Create(SaveMeetingRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPut("{id:guid}")] public Task<MeetingDto> Update(Guid id, SaveMeetingRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Cancel(Guid id, [FromQuery] long version, CancellationToken ct) { await service.CancelAsync(id, version, ct); return NoContent(); }
}
