using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed class SelfService(
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IRepository<Employee> employees,
    IRepository<Tenant> tenants,
    IRepository<AttendanceRecord> attendance,
    IRepository<LeaveRequest> leaveRequests,
    IRepository<TimesheetEntry> timesheets,
    IRepository<ExpenseClaim> expenses,
    IRepository<TrainingEnrollment> enrollments,
    IRepository<PerformanceReview> reviews,
    IRepository<Asset> assets,
    IRepository<AssetAssignment> assignments,
    IRepository<PayrollRun> payrollRuns,
    IRepository<PayrollItem> payrollItems,
    IRepository<UserAccount> users,
    IRepository<RefreshToken> refreshTokens,
    IAttendanceService attendanceService,
    ILeaveService leaveService,
    IWorkforceOperationsService workforceService,
    IExpenseService expenseService,
    ITrainingService trainingService,
    IPerformanceService performanceService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ISelfService
{
    private Guid EmployeeId => currentUser.EmployeeId ?? throw new UnauthorizedAccessException("This account is not linked to an employee record.");

    public async Task<SelfProfileDto> GetProfileAsync(CancellationToken ct) => MapProfile(await Employee(ct));

    public async Task<SelfProfileDto> UpdateProfileAsync(UpdateSelfProfileRequest request, CancellationToken ct)
    {
        var employee = await Employee(ct);
        var phone = request.Phone?.Trim();
        if (phone?.Length > 40) throw new DomainException("Phone number cannot exceed 40 characters.");
        employee.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone;
        await unitOfWork.SaveChangesAsync(ct);
        return MapProfile(employee);
    }

    public async Task<SelfDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var employee = await Employee(ct);
        var today = await TenantToday(ct);
        var todayRows = await attendance.ListAsync(x => x.EmployeeId == employee.Id && x.WorkDate == today, q => q.OrderByDescending(x => x.ClockedInAt), cancellationToken: ct);
        // An open session can have started before the tenant's current date (for
        // example, when an employee forgot to check out). Clock-in validation
        // considers that session open, so the self-service dashboard must surface
        // it as well; otherwise it incorrectly offers another check-in that the
        // API will reject.
        var openRows = await attendance.ListAsync(
            x => x.EmployeeId == employee.Id && x.ClockedOutAt == null,
            q => q.OrderByDescending(x => x.ClockedInAt),
            take: 1,
            cancellationToken: ct);
        var todayAttendance = openRows.FirstOrDefault() ?? todayRows.FirstOrDefault();
        var todayTotalHours = Math.Round(todayRows.Sum(x => x.WorkHours) + todayRows.Where(x => x.ClockedOutAt == null && x.ClockedInAt.HasValue).Sum(x => Math.Max(0, (decimal)(DateTimeOffset.UtcNow - x.ClockedInAt!.Value).TotalHours)), 2);
        var pendingLeave = await leaveRequests.CountAsync(x => x.EmployeeId == employee.Id && x.Status == LeaveRequestStatus.Pending, ct);
        var balances = await leaveService.GetBalancesAsync(employee.Id, today.Year, ct);
        var pendingTimesheets = await timesheets.CountAsync(x => x.EmployeeId == employee.Id && x.Status == WorkflowStatus.Pending, ct);
        var openExpenses = await expenses.CountAsync(x => x.EmployeeId == employee.Id && (x.Status == ExpenseStatus.Draft || x.Status == ExpenseStatus.Submitted), ct);
        var trainingDue = await enrollments.CountAsync(x => x.EmployeeId == employee.Id && x.Status != EnrollmentStatus.Completed && x.Status != EnrollmentStatus.Cancelled, ct);
        var news = await GetAnnouncementsAsync(ct);
        var policy = (await attendanceService.GetPolicyAsync(ct))[0];
        return new(MapProfile(employee), todayAttendance is null ? null : MapPrivateAttendance(todayAttendance), todayTotalHours, todayRows.Count, policy.RequireLocationCapture, pendingLeave, balances.Sum(x => x.Available), pendingTimesheets, openExpenses, trainingDue, news.Take(5).ToArray());
    }

    public async Task<AttendanceDto> ClockInAsync(SelfClockRequest r, string? ip, string? agent, CancellationToken ct) { await EnforceLocationPolicy(r, ct); return RemoveLocation(await attendanceService.ClockInAsync(ToClock(r, ip, agent), ct)); }
    public async Task<AttendanceDto> ClockOutAsync(SelfClockRequest r, string? ip, string? agent, CancellationToken ct) { await EnforceLocationPolicy(r, ct); return RemoveLocation(await attendanceService.ClockOutAsync(ToClock(r, ip, agent), ct)); }
    public async Task<PagedResult<AttendanceDto>> GetAttendanceAsync(PagedRequest r, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var result = await attendanceService.SearchAsync(r, EmployeeId, from, to, ct);
        return new(result.Items.Select(RemoveLocation).ToArray(), result.Page, result.PageSize, result.Total);
    }
    public Task<PagedResult<DailyAttendanceReportDto>> GetAttendanceReportAsync(PagedRequest r, DateOnly? from, DateOnly? to, CancellationToken ct) => attendanceService.ReportAsync(r, EmployeeId, from, to, ct);
    public Task<IReadOnlyList<AttendanceSummaryDto>> GetAttendanceSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct) => attendanceService.SummaryAsync(EmployeeId, from, to, ct);

    public Task<LeaveRequestDto> SubmitLeaveAsync(SelfLeaveRequest r, CancellationToken ct) => leaveService.SubmitAsync(new(EmployeeId, r.LeaveTypeId, r.StartsOn, r.EndsOn, r.Days, r.Reason), ct);
    public Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken ct) => leaveService.ListTypesAsync(ct);
    public Task<PagedResult<LeaveRequestDto>> GetLeaveAsync(PagedRequest r, LeaveRequestStatus? status, CancellationToken ct) => leaveService.SearchAsync(r, EmployeeId, status, ct);
    public Task<IReadOnlyList<LeaveBalanceDto>> GetLeaveBalancesAsync(int year, CancellationToken ct) => leaveService.GetBalancesAsync(EmployeeId, year, ct);

    public Task<TimesheetDto> SubmitTimesheetAsync(SelfTimesheetRequest r, CancellationToken ct) => workforceService.SubmitTimesheetAsync(new(EmployeeId, r.WorkDate, r.Description, r.Hours, r.ProjectCode), ct);
    public Task<PagedResult<TimesheetDto>> GetTimesheetsAsync(PagedRequest r, WorkflowStatus? status, CancellationToken ct) => workforceService.SearchTimesheetsAsync(r, EmployeeId, status, ct);

    public Task<ExpenseDto> CreateExpenseAsync(SelfExpenseRequest r, CancellationToken ct) => expenseService.CreateAsync(new(EmployeeId, r.Category, r.ExpenseDate, r.Amount, r.Currency, r.Description, r.ReceiptStorageKey), ct);
    public async Task<PagedResult<ExpenseDto>> GetExpensesAsync(PagedRequest r, ExpenseStatus? status, CancellationToken ct) => await expenseService.SearchAsync(r, EmployeeId, status, ct);
    public async Task<ExpenseDto> SubmitExpenseAsync(Guid id, long version, CancellationToken ct) { await EnsureOwner(expenses, id, x => x.EmployeeId, ct); return await expenseService.SubmitAsync(id, version, ct); }

    public Task<IReadOnlyList<TrainingEnrollmentDto>> GetTrainingAsync(CancellationToken ct) => trainingService.ListEnrollmentsAsync(EmployeeId, ct);
    public Task<IReadOnlyList<TrainingCourseDto>> GetTrainingCoursesAsync(CancellationToken ct) => trainingService.ListCoursesAsync(ct);
    public async Task<TrainingEnrollmentDto> CompleteTrainingAsync(Guid id, CompleteTrainingRequest r, CancellationToken ct) { await EnsureOwner(enrollments, id, x => x.EmployeeId, ct); return await trainingService.CompleteAsync(id, r, ct); }

    public Task<IReadOnlyList<PerformanceReviewDto>> GetPerformanceAsync(CancellationToken ct) => performanceService.ListReviewsAsync(EmployeeId, null, ct);
    public async Task<PerformanceReviewDto> UpdatePerformanceAsync(Guid id, UpdateReviewRequest r, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Review not found.");
        if (review.EmployeeId != EmployeeId) throw new UnauthorizedAccessException("You can only update your own review.");
        if (r.Status is not (ReviewStatus.SelfReview or ReviewStatus.ManagerReview)) throw new DomainException("Employees can only submit the self-review stage.");
        return await performanceService.UpdateReviewAsync(id, r with { ManagerRating = review.ManagerRating }, ct);
    }

    public async Task<IReadOnlyList<AssetDto>> GetAssetsAsync(CancellationToken ct)
    {
        var links = await assignments.ListAsync(x => x.EmployeeId == EmployeeId && x.ReturnedAt == null, cancellationToken: ct);
        var ids = links.Select(x => x.AssetId).ToHashSet();
        return (await assets.ListAsync(x => ids.Contains(x.Id), q => q.OrderBy(x => x.AssetTag), cancellationToken: ct)).Select(MapAsset).ToArray();
    }
    public Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(CancellationToken ct) => workforceService.ListDocumentsAsync(EmployeeId, ct);
    public Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken ct) => workforceService.ListAnnouncementsAsync(ct);

    public async Task<IReadOnlyList<PayslipDto>> GetPayslipsAsync(CancellationToken ct)
    {
        var items = await payrollItems.ListAsync(x => x.EmployeeId == EmployeeId, q => q.OrderByDescending(x => x.CreatedAt), cancellationToken: ct);
        var result = new List<PayslipDto>();
        foreach (var item in items)
        {
            var run = await payrollRuns.GetByIdAsync(item.PayrollRunId, ct);
            if (run is null || run.Status is not (PayrollRunStatus.Approved or PayrollRunStatus.Paid)) continue;
            result.Add(new(run.Id, run.Name, run.PeriodStart, run.PeriodEnd, run.PaymentDate, run.Currency, item.BasicPay, item.Allowances, item.OvertimePay, item.Deductions, item.Taxes, item.GrossPay, item.NetPay, run.Status));
        }
        return result;
    }

    public async Task<IReadOnlyList<TeamMemberDto>> GetTeamAsync(CancellationToken ct)
    {
        EnsureTeamPermission(Permissions.TeamRead);
        return (await employees.ListAsync(x => x.ManagerId == EmployeeId, q => q.OrderBy(x => x.FirstName).ThenBy(x => x.LastName), cancellationToken: ct)).Select(x => new TeamMemberDto(x.Id, x.EmployeeNumber, x.FullName, x.WorkEmail, x.Status, x.DepartmentId, x.DesignationId)).ToArray();
    }

    public async Task<PagedResult<LeaveRequestDto>> GetTeamLeaveAsync(PagedRequest r, LeaveRequestStatus? status, CancellationToken ct)
    {
        var ids = await TeamIds(ct); return await PageLeave(ids, r, status, ct);
    }
    public async Task<LeaveRequestDto> ReviewTeamLeaveAsync(Guid id, ReviewLeaveRequest r, CancellationToken ct) { EnsureTeamPermission(Permissions.TeamApprove); var item = await leaveRequests.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Leave request not found."); await EnsureDirectReport(item.EmployeeId, ct); return await leaveService.ReviewAsync(id, r, ct); }

    public async Task<PagedResult<AttendanceDto>> GetTeamAttendanceAsync(PagedRequest r, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var ids = await TeamIds(ct); System.Linq.Expressions.Expression<Func<AttendanceRecord, bool>> p = x => ids.Contains(x.EmployeeId) && (!from.HasValue || x.WorkDate >= from) && (!to.HasValue || x.WorkDate <= to); var total = await attendance.CountAsync(p, ct); var rows = await attendance.ListAsync(p, q => q.OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.ClockedInAt), r.Skip, r.SafePageSize, ct); return new(rows.Select(AttendanceService.Map).ToArray(), r.SafePage, r.SafePageSize, total);
    }
    public async Task<PagedResult<TimesheetDto>> GetTeamTimesheetsAsync(PagedRequest r, WorkflowStatus? status, CancellationToken ct)
    {
        var ids = await TeamIds(ct); System.Linq.Expressions.Expression<Func<TimesheetEntry, bool>> p = x => ids.Contains(x.EmployeeId) && (!status.HasValue || x.Status == status); var total = await timesheets.CountAsync(p, ct); var rows = await timesheets.ListAsync(p, q => q.OrderByDescending(x => x.WorkDate), r.Skip, r.SafePageSize, ct); return new(rows.Select(MapTimesheet).ToArray(), r.SafePage, r.SafePageSize, total);
    }
    public async Task<TimesheetDto> ReviewTeamTimesheetAsync(Guid id, ReviewTimesheetRequest r, CancellationToken ct) { EnsureTeamPermission(Permissions.TeamApprove); var item = await timesheets.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Timesheet not found."); await EnsureDirectReport(item.EmployeeId, ct); return await workforceService.ReviewTimesheetAsync(id, r, ct); }
    public async Task<PagedResult<ExpenseDto>> GetTeamExpensesAsync(PagedRequest r, ExpenseStatus? status, CancellationToken ct)
    {
        var ids = await TeamIds(ct); System.Linq.Expressions.Expression<Func<ExpenseClaim, bool>> p = x => ids.Contains(x.EmployeeId) && (!status.HasValue || x.Status == status); var total = await expenses.CountAsync(p, ct); var rows = await expenses.ListAsync(p, q => q.OrderByDescending(x => x.ExpenseDate), r.Skip, r.SafePageSize, ct); return new(rows.Select(MapExpense).ToArray(), r.SafePage, r.SafePageSize, total);
    }
    public async Task<ExpenseDto> ReviewTeamExpenseAsync(Guid id, ReviewExpenseRequest r, CancellationToken ct) { EnsureTeamPermission(Permissions.TeamApprove); var item = await expenses.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Expense not found."); await EnsureDirectReport(item.EmployeeId, ct); return await expenseService.ReviewAsync(id, r, ct); }
    public async Task<IReadOnlyList<PerformanceReviewDto>> GetTeamPerformanceAsync(CancellationToken ct)
    {
        var ids = await TeamIds(ct); return (await reviews.ListAsync(x => ids.Contains(x.EmployeeId) && x.ReviewerId == EmployeeId, q => q.OrderByDescending(x => x.CreatedAt), cancellationToken: ct)).Select(x => new PerformanceReviewDto(x.Id, x.CycleId, x.EmployeeId, x.ReviewerId, x.Status, x.SelfRating, x.ManagerRating, x.GoalsJson, x.Feedback, x.Version)).ToArray();
    }
    public async Task<PerformanceReviewDto> UpdateTeamPerformanceAsync(Guid id, UpdateReviewRequest r, CancellationToken ct)
    {
        EnsureTeamPermission(Permissions.TeamApprove); var item = await reviews.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Review not found."); await EnsureDirectReport(item.EmployeeId, ct); if (item.ReviewerId != EmployeeId) throw new UnauthorizedAccessException("You are not the assigned reviewer."); if (r.Status is not (ReviewStatus.ManagerReview or ReviewStatus.Completed)) throw new DomainException("Managers can only submit manager review or completion stages."); return await performanceService.UpdateReviewAsync(id, r with { SelfRating = item.SelfRating }, ct);
    }
    public async Task ChangePasswordAsync(ChangePasswordRequest r, CancellationToken ct)
    {
        if (r.NewPassword.Length < 8) throw new DomainException("New password must be at least 8 characters.");
        if (r.NewPassword == r.CurrentPassword) throw new DomainException("New password must be different from the current password.");
        var user = await users.GetByIdAsync(currentUser.UserId ?? throw new UnauthorizedAccessException("User identity is missing."), ct) ?? throw new KeyNotFoundException("User not found.");
        if (!passwordHasher.Verify(r.CurrentPassword, user.PasswordHash)) throw new DomainException("Current password is incorrect.");
        user.PasswordHash = passwordHasher.Hash(r.NewPassword);
        foreach (var token in await refreshTokens.ListAsync(x => x.UserId == user.Id && x.RevokedAt == null, cancellationToken: ct)) token.RevokedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Employee> Employee(CancellationToken ct) => await employees.GetByIdAsync(EmployeeId, ct) ?? throw new UnauthorizedAccessException("Linked employee record no longer exists.");
    private async Task<DateOnly> TenantToday(CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(currentTenant.TenantId ?? throw new UnauthorizedAccessException("Tenant identity is missing."), ct);
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tenant?.TimeZone ?? "UTC")).DateTime); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(DateTime.UtcNow); }
        catch (InvalidTimeZoneException) { return DateOnly.FromDateTime(DateTime.UtcNow); }
    }
    private ClockRequest ToClock(SelfClockRequest r, string? ip, string? agent) => new(EmployeeId, null, "web", r.Notes, r.Latitude, r.Longitude, r.AccuracyMeters, r.Address, ip, agent);
    private static AttendanceDto MapPrivateAttendance(AttendanceRecord record) => RemoveLocation(AttendanceService.Map(record));
    private static AttendanceDto RemoveLocation(AttendanceDto item) => item with
    {
        ClockInLatitude = null, ClockInLongitude = null, ClockInAccuracyMeters = null, ClockInAddress = null,
        ClockInIpAddress = null, ClockInUserAgent = null, ClockOutLatitude = null, ClockOutLongitude = null,
        ClockOutAccuracyMeters = null, ClockOutAddress = null, ClockOutIpAddress = null, ClockOutUserAgent = null
    };
    private async Task EnforceLocationPolicy(SelfClockRequest r, CancellationToken ct)
    {
        var policy = (await attendanceService.GetPolicyAsync(ct))[0];
        if (policy.RequireLocationCapture && (!r.Latitude.HasValue || !r.Longitude.HasValue || !r.AccuracyMeters.HasValue)) throw new DomainException("Your company requires location for check-in and check-out.");
    }
    private static SelfProfileDto MapProfile(Employee x) => new(x.Id, x.EmployeeNumber, x.FullName, x.WorkEmail, x.Phone, x.HireDate, x.Status, x.EmploymentType, x.DepartmentId, x.DesignationId, x.LocationId, x.ManagerId, x.SalaryCurrency, x.BaseSalary);
    private static AssetDto MapAsset(Asset x) => new(x.Id, x.AssetTag, x.Name, x.Category, x.SerialNumber, x.Status);
    private static TimesheetDto MapTimesheet(TimesheetEntry x) => new(x.Id, x.EmployeeId, x.WorkDate, x.ProjectCode, x.Description, x.Hours, x.Status, x.Version);
    private static ExpenseDto MapExpense(ExpenseClaim x) => new(x.Id, x.EmployeeId, x.ClaimNumber, x.Category, x.ExpenseDate, x.Amount, x.Currency, x.Description, x.Status, x.Version);
    private static LeaveRequestDto MapLeave(LeaveRequest x) => new(x.Id, x.EmployeeId, x.LeaveTypeId, x.StartsOn, x.EndsOn, x.Days, x.Reason, x.Status, x.ReviewComment, x.CreatedAt, x.ReviewedAt, x.Version);
    private async Task<HashSet<Guid>> TeamIds(CancellationToken ct) { EnsureTeamPermission(Permissions.TeamRead); return (await employees.ListAsync(x => x.ManagerId == EmployeeId, cancellationToken: ct)).Select(x => x.Id).ToHashSet(); }
    private void EnsureTeamPermission(string permission) { if (!currentUser.HasPermission(permission)) throw new UnauthorizedAccessException("Your role does not allow access to team data."); }
    private async Task EnsureDirectReport(Guid employeeId, CancellationToken ct) { var employee = await employees.GetByIdAsync(employeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); if (employee.ManagerId != EmployeeId) throw new UnauthorizedAccessException("This employee is not your direct report."); }
    private async Task<PagedResult<LeaveRequestDto>> PageLeave(HashSet<Guid> ids, PagedRequest r, LeaveRequestStatus? status, CancellationToken ct) { var query = r.Search?.Trim().ToLowerInvariant(); System.Linq.Expressions.Expression<Func<LeaveRequest, bool>> p = x => ids.Contains(x.EmployeeId) && (!status.HasValue || x.Status == status) && (string.IsNullOrEmpty(query) || x.Reason.ToLower().Contains(query)); var total = await leaveRequests.CountAsync(p, ct); var rows = await leaveRequests.ListAsync(p, q => q.OrderBy(x => x.Status == LeaveRequestStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatedAt), r.Skip, r.SafePageSize, ct); return new(rows.Select(MapLeave).ToArray(), r.SafePage, r.SafePageSize, total); }
    private async Task EnsureOwner<T>(IRepository<T> repository, Guid id, Func<T, Guid> owner, CancellationToken ct) where T : Hrms.Domain.Common.AuditableEntity { var item = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Record not found."); if (owner(item) != EmployeeId) throw new UnauthorizedAccessException("You can only modify your own record."); }
}
