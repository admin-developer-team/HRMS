using Hrms.Application;
using Hrms.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/employees"), Authorize(Policy = Permissions.EmployeesRead)]
public sealed class EmployeesController(IEmployeeService service) : ControllerBase
{
    [HttpPost, Authorize(Policy = Permissions.EmployeesManage)] public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken ct) { var x = await service.CreateAsync(request, ct); return CreatedAtAction(nameof(Get), new { id = x.Id }, x); }
    [HttpGet("{id:guid}")] public Task<EmployeeDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpGet("{id:guid}/login-history")] public Task<IReadOnlyList<LoginHistoryDto>> LoginHistory(Guid id, [FromQuery] int take = 50, CancellationToken ct = default) => service.GetLoginHistoryAsync(id, take, ct);
    [HttpGet] public Task<PagedResult<EmployeeDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] EmploymentStatus? status = null, [FromQuery] Guid? departmentId = null, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize, search), status, departmentId, ct);
    [HttpPut("{id:guid}"), Authorize(Policy = Permissions.EmployeesManage)] public Task<EmployeeDto> Update(Guid id, UpdateEmployeeRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);
    [HttpDelete("{id:guid}"), Authorize(Policy = Permissions.EmployeesManage)] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/v1/organization"), Authorize(Policy = Permissions.EmployeesRead)]
public sealed class OrganizationController(IOrganizationService service) : ControllerBase
{
    [HttpPost("departments"), Authorize(Policy = Permissions.OrganizationManage)] public Task<DepartmentDto> CreateDepartment(CreateDepartmentRequest request, CancellationToken ct) => service.CreateDepartmentAsync(request, ct);
    [HttpGet("departments")] public Task<IReadOnlyList<DepartmentDto>> Departments(CancellationToken ct) => service.ListDepartmentsAsync(ct);
    [HttpPost("designations"), Authorize(Policy = Permissions.OrganizationManage)] public Task<DesignationDto> CreateDesignation(CreateDesignationRequest request, CancellationToken ct) => service.CreateDesignationAsync(request, ct);
    [HttpGet("designations")] public Task<IReadOnlyList<DesignationDto>> Designations(CancellationToken ct) => service.ListDesignationsAsync(ct);
    [HttpPost("locations"), Authorize(Policy = Permissions.OrganizationManage)] public Task<LocationDto> CreateLocation(CreateLocationRequest request, CancellationToken ct) => service.CreateLocationAsync(request, ct);
    [HttpGet("locations")] public Task<IReadOnlyList<LocationDto>> Locations(CancellationToken ct) => service.ListLocationsAsync(ct);
}

[ApiController, Route("api/v1/leave"), Authorize(Policy = Permissions.LeaveManage)]
public sealed class LeaveController(ILeaveService service) : ControllerBase
{
    [HttpPost("types")] public Task<LeaveTypeDto> CreateType(CreateLeaveTypeRequest request, CancellationToken ct) => service.CreateTypeAsync(request, ct);
    [HttpGet("types")] public Task<IReadOnlyList<LeaveTypeDto>> Types(CancellationToken ct) => service.ListTypesAsync(ct);
    [HttpPost("requests")] public Task<LeaveRequestDto> Submit(SubmitLeaveRequest request, CancellationToken ct) => service.SubmitAsync(request, ct);
    [HttpPut("requests/{id:guid}/review")] public Task<LeaveRequestDto> Review(Guid id, ReviewLeaveRequest request, CancellationToken ct) => service.ReviewAsync(id, request, ct);
    [HttpGet("requests")] public Task<PagedResult<LeaveRequestDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? employeeId = null, [FromQuery] LeaveRequestStatus? status = null, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize), employeeId, status, ct);
    [HttpGet("balances/{employeeId:guid}")] public Task<IReadOnlyList<LeaveBalanceDto>> Balances(Guid employeeId, [FromQuery] int? year = null, CancellationToken ct = default) => service.GetBalancesAsync(employeeId, year ?? DateTime.UtcNow.Year, ct);
}

[ApiController, Route("api/v1/attendance"), Authorize(Policy = Permissions.AttendanceManage)]
public sealed class AttendanceController(IAttendanceService service) : ControllerBase
{
    [HttpPost("clock-in")] public Task<AttendanceDto> ClockIn(ClockRequest request, CancellationToken ct) => service.ClockInAsync(Enrich(request), ct);
    [HttpPost("clock-out")] public Task<AttendanceDto> ClockOut(ClockRequest request, CancellationToken ct) => service.ClockOutAsync(Enrich(request), ct);
    [HttpGet] public Task<PagedResult<AttendanceDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? employeeId = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize), employeeId, from, to, ct);
    [HttpGet("policy")] public Task<IReadOnlyList<AttendancePolicyDto>> Policy(CancellationToken ct) => service.GetPolicyAsync(ct);
    [HttpPut("policy")] public Task<AttendancePolicyDto> UpdatePolicy(UpdateAttendancePolicyRequest request, CancellationToken ct) => service.UpdatePolicyAsync(request, ct);
    [HttpGet("report")] public Task<PagedResult<DailyAttendanceReportDto>> Report([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? employeeId = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.ReportAsync(new(page, pageSize), employeeId, from, to, ct);
    [HttpGet("summary")] public Task<IReadOnlyList<AttendanceSummaryDto>> Summary([FromQuery] Guid? employeeId = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.SummaryAsync(employeeId, from, to, ct);
    private ClockRequest Enrich(ClockRequest request) => request with { Source = "admin", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = Request.Headers.UserAgent.ToString() };
}

[ApiController, Route("api/v1/workforce"), Authorize(Policy = Permissions.WorkforceManage)]
public sealed class WorkforceOperationsController(IWorkforceOperationsService service) : ControllerBase
{
    [HttpPost("shifts")] public Task<ShiftDto> CreateShift(CreateShiftRequest request, CancellationToken ct) => service.CreateShiftAsync(request, ct);
    [HttpGet("shifts")] public Task<IReadOnlyList<ShiftDto>> Shifts(CancellationToken ct) => service.ListShiftsAsync(ct);
    [HttpPost("holidays")] public Task<HolidayDto> CreateHoliday(CreateHolidayRequest request, CancellationToken ct) => service.CreateHolidayAsync(request, ct);
    [HttpGet("holidays")] public Task<IReadOnlyList<HolidayDto>> Holidays([FromQuery] int? year = null, CancellationToken ct = default) => service.ListHolidaysAsync(year ?? DateTime.UtcNow.Year, ct);
    [HttpPost("timesheets")] public Task<TimesheetDto> SubmitTimesheet(SubmitTimesheetRequest request, CancellationToken ct) => service.SubmitTimesheetAsync(request, ct);
    [HttpPut("timesheets/{id:guid}/review")] public Task<TimesheetDto> ReviewTimesheet(Guid id, ReviewTimesheetRequest request, CancellationToken ct) => service.ReviewTimesheetAsync(id, request, ct);
    [HttpGet("timesheets")] public Task<PagedResult<TimesheetDto>> Timesheets([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? employeeId = null, [FromQuery] WorkflowStatus? status = null, CancellationToken ct = default) => service.SearchTimesheetsAsync(new(page, pageSize), employeeId, status, ct);
    [HttpPost("documents")] public Task<EmployeeDocumentDto> AddDocument(AddEmployeeDocumentRequest request, CancellationToken ct) => service.AddDocumentAsync(request, ct);
    [HttpPut("documents/{id:guid}/verify")] public Task<EmployeeDocumentDto> VerifyDocument(Guid id, VerifyDocumentRequest request, CancellationToken ct) => service.VerifyDocumentAsync(id, request, ct);
    [HttpGet("employees/{employeeId:guid}/documents")] public Task<IReadOnlyList<EmployeeDocumentDto>> Documents(Guid employeeId, CancellationToken ct) => service.ListDocumentsAsync(employeeId, ct);
    [HttpPost("announcements")] public Task<AnnouncementDto> CreateAnnouncement(CreateAnnouncementRequest request, CancellationToken ct) => service.CreateAnnouncementAsync(request, ct);
    [HttpGet("announcements")] public Task<IReadOnlyList<AnnouncementDto>> Announcements(CancellationToken ct) => service.ListAnnouncementsAsync(ct);
}
