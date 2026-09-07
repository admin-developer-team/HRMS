using Hrms.Application;
using Hrms.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/me/leave"), Authorize(Policy = Permissions.SelfService)]
public sealed class SelfLeaveCancellationController(ILeaveService service) : ControllerBase
{
    [HttpPut("{id:guid}/cancel")]
    public Task<LeaveRequestDto> Cancel(Guid id, [FromQuery] long version, CancellationToken ct) => service.CancelAsync(id, version, ct);
}

[ApiController, Route("api/v1/me"), Authorize(Policy = "EmployeeLinked"), Authorize(Policy = Permissions.SelfService)]
public sealed class SelfServiceController(ISelfService service) : ControllerBase
{
    [HttpGet] public Task<SelfProfileDto> Profile(CancellationToken ct) => service.GetProfileAsync(ct);
    [HttpPut] public Task<SelfProfileDto> UpdateProfile(UpdateSelfProfileRequest request, CancellationToken ct) => service.UpdateProfileAsync(request, ct);
    [HttpGet("dashboard")] public Task<SelfDashboardDto> Dashboard(CancellationToken ct) => service.GetDashboardAsync(ct);
    [HttpPost("attendance/clock-in")] public Task<AttendanceDto> ClockIn(SelfClockRequest request, CancellationToken ct) => service.ClockInAsync(request, IpAddress(), Request.Headers.UserAgent.ToString(), ct);
    [HttpPost("attendance/clock-out")] public Task<AttendanceDto> ClockOut(SelfClockRequest request, CancellationToken ct) => service.ClockOutAsync(request, IpAddress(), Request.Headers.UserAgent.ToString(), ct);
    [HttpGet("attendance")] public Task<PagedResult<AttendanceDto>> Attendance([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.GetAttendanceAsync(new(page, pageSize), from, to, ct);
    [HttpGet("attendance/report")] public Task<PagedResult<DailyAttendanceReportDto>> AttendanceReport([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.GetAttendanceReportAsync(new(page, pageSize), from, to, ct);
    [HttpGet("attendance/summary")] public Task<IReadOnlyList<AttendanceSummaryDto>> AttendanceSummary([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.GetAttendanceSummaryAsync(from, to, ct);

    [HttpPost("leave")] public Task<LeaveRequestDto> SubmitLeave(SelfLeaveRequest request, CancellationToken ct) => service.SubmitLeaveAsync(request, ct);
    [HttpGet("leave-types")] public Task<IReadOnlyList<LeaveTypeDto>> LeaveTypes(CancellationToken ct) => service.GetLeaveTypesAsync(ct);
    [HttpGet("leave")] public Task<PagedResult<LeaveRequestDto>> Leave([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] LeaveRequestStatus? status = null, CancellationToken ct = default) => service.GetLeaveAsync(new(page, pageSize), status, ct);
    [HttpGet("leave-balances")] public Task<IReadOnlyList<LeaveBalanceDto>> LeaveBalances([FromQuery] int? year = null, CancellationToken ct = default) => service.GetLeaveBalancesAsync(year ?? DateTime.UtcNow.Year, ct);

    [HttpPost("timesheets")] public Task<TimesheetDto> SubmitTimesheet(SelfTimesheetRequest request, CancellationToken ct) => service.SubmitTimesheetAsync(request, ct);
    [HttpGet("timesheets")] public Task<PagedResult<TimesheetDto>> Timesheets([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] WorkflowStatus? status = null, CancellationToken ct = default) => service.GetTimesheetsAsync(new(page, pageSize), status, ct);

    [HttpPost("expenses")] public Task<ExpenseDto> CreateExpense(SelfExpenseRequest request, CancellationToken ct) => service.CreateExpenseAsync(request, ct);
    [HttpGet("expenses")] public Task<PagedResult<ExpenseDto>> Expenses([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] ExpenseStatus? status = null, CancellationToken ct = default) => service.GetExpensesAsync(new(page, pageSize), status, ct);
    [HttpPost("expenses/{id:guid}/submit")] public Task<ExpenseDto> SubmitExpense(Guid id, [FromQuery] long version, CancellationToken ct) => service.SubmitExpenseAsync(id, version, ct);

    [HttpGet("training")] public Task<IReadOnlyList<TrainingEnrollmentDto>> Training(CancellationToken ct) => service.GetTrainingAsync(ct);
    [HttpGet("training-courses")] public Task<IReadOnlyList<TrainingCourseDto>> TrainingCourses(CancellationToken ct) => service.GetTrainingCoursesAsync(ct);
    [HttpPut("training/{id:guid}/complete")] public Task<TrainingEnrollmentDto> CompleteTraining(Guid id, CompleteTrainingRequest request, CancellationToken ct) => service.CompleteTrainingAsync(id, request, ct);
    [HttpGet("performance")] public Task<IReadOnlyList<PerformanceReviewDto>> Performance(CancellationToken ct) => service.GetPerformanceAsync(ct);
    [HttpPut("performance/{id:guid}")] public Task<PerformanceReviewDto> UpdatePerformance(Guid id, UpdateReviewRequest request, CancellationToken ct) => service.UpdatePerformanceAsync(id, request, ct);
    [HttpGet("assets")] public Task<IReadOnlyList<AssetDto>> Assets(CancellationToken ct) => service.GetAssetsAsync(ct);
    [HttpGet("documents")] public Task<IReadOnlyList<EmployeeDocumentDto>> Documents(CancellationToken ct) => service.GetDocumentsAsync(ct);
    [HttpGet("announcements")] public Task<IReadOnlyList<AnnouncementDto>> Announcements(CancellationToken ct) => service.GetAnnouncementsAsync(ct);
    [HttpGet("payslips")] public Task<IReadOnlyList<PayslipDto>> Payslips(CancellationToken ct) => service.GetPayslipsAsync(ct);
    [HttpPost("change-password")] public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) { await service.ChangePasswordAsync(request, ct); return NoContent(); }

    [HttpGet("team"), Authorize(Policy = Permissions.TeamRead)] public Task<IReadOnlyList<TeamMemberDto>> Team(CancellationToken ct) => service.GetTeamAsync(ct);
    [HttpGet("team/leave"), Authorize(Policy = Permissions.TeamRead)] public Task<PagedResult<LeaveRequestDto>> TeamLeave([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] LeaveRequestStatus? status = null, CancellationToken ct = default) => service.GetTeamLeaveAsync(new(page, pageSize), status, ct);
    [HttpPut("team/leave/{id:guid}/review"), Authorize(Policy = Permissions.TeamApprove)] public Task<LeaveRequestDto> ReviewTeamLeave(Guid id, ReviewLeaveRequest request, CancellationToken ct) => service.ReviewTeamLeaveAsync(id, request, ct);
    [HttpGet("team/attendance"), Authorize(Policy = Permissions.TeamRead)] public Task<PagedResult<AttendanceDto>> TeamAttendance([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => service.GetTeamAttendanceAsync(new(page, pageSize), from, to, ct);
    [HttpGet("team/timesheets"), Authorize(Policy = Permissions.TeamRead)] public Task<PagedResult<TimesheetDto>> TeamTimesheets([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] WorkflowStatus? status = null, CancellationToken ct = default) => service.GetTeamTimesheetsAsync(new(page, pageSize), status, ct);
    [HttpPut("team/timesheets/{id:guid}/review"), Authorize(Policy = Permissions.TeamApprove)] public Task<TimesheetDto> ReviewTeamTimesheet(Guid id, ReviewTimesheetRequest request, CancellationToken ct) => service.ReviewTeamTimesheetAsync(id, request, ct);
    [HttpGet("team/expenses"), Authorize(Policy = Permissions.TeamRead)] public Task<PagedResult<ExpenseDto>> TeamExpenses([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] ExpenseStatus? status = null, CancellationToken ct = default) => service.GetTeamExpensesAsync(new(page, pageSize), status, ct);
    [HttpPut("team/expenses/{id:guid}/review"), Authorize(Policy = Permissions.TeamApprove)] public Task<ExpenseDto> ReviewTeamExpense(Guid id, ReviewExpenseRequest request, CancellationToken ct) => service.ReviewTeamExpenseAsync(id, request, ct);
    [HttpGet("team/performance"), Authorize(Policy = Permissions.TeamRead)] public Task<IReadOnlyList<PerformanceReviewDto>> TeamPerformance(CancellationToken ct) => service.GetTeamPerformanceAsync(ct);
    [HttpPut("team/performance/{id:guid}"), Authorize(Policy = Permissions.TeamApprove)] public Task<PerformanceReviewDto> UpdateTeamPerformance(Guid id, UpdateReviewRequest request, CancellationToken ct) => service.UpdateTeamPerformanceAsync(id, request, ct);

    private string? IpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
