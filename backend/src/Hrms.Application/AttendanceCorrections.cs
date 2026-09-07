using System.Linq.Expressions;
using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record RequestAttendanceCorrection(Guid? EmployeeId, Guid? AttendanceRecordId, DateOnly WorkDate,
    DateTimeOffset RequestedClockIn, DateTimeOffset RequestedClockOut, string Reason);
public sealed record ReviewAttendanceCorrection(bool Approve, string? Comment, long Version);
public sealed record AttendanceCorrectionDto(Guid Id, Guid EmployeeId, string EmployeeName, Guid? AttendanceRecordId,
    DateOnly WorkDate, DateTimeOffset RequestedClockIn, DateTimeOffset RequestedClockOut, string Reason,
    WorkflowStatus Status, string? ReviewComment, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt, long Version);

public sealed class AttendanceCorrectionService(IRepository<AttendanceCorrection> corrections,
    IRepository<AttendanceRecord> records, IRepository<Employee> employees, IRepository<AttendancePolicy> policies,
    IRepository<Tenant> tenants, IRepository<Holiday> holidays, IRepository<LeaveRequest> leave,
    ICurrentTenant tenant, ICurrentUser user, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant)
{
    public async Task<PagedResult<AttendanceCorrectionDto>> SearchAsync(PagedRequest page, string scope, WorkflowStatus? status, CancellationToken ct)
    {
        var people = await employees.ListAsync(cancellationToken: ct);
        Guid[] ids = scope switch
        {
            "all" when user.HasPermission(Permissions.AttendanceManage) => people.Select(x => x.Id).ToArray(),
            "team" when user.HasPermission(Permissions.TeamRead) && user.EmployeeId.HasValue => people.Where(x => x.ManagerId == user.EmployeeId && x.Id != user.EmployeeId).Select(x => x.Id).ToArray(),
            "mine" when user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue => [user.EmployeeId.Value],
            _ => throw new UnauthorizedAccessException("You cannot access these attendance corrections.")
        };
        Expression<Func<AttendanceCorrection, bool>> predicate = x => ids.Contains(x.EmployeeId) && (!status.HasValue || x.Status == status);
        var total = await corrections.CountAsync(predicate, ct);
        var rows = await corrections.ListAsync(predicate, q => q.OrderByDescending(x => x.CreatedAt), page.Skip, page.SafePageSize, ct);
        var names = people.ToDictionary(x => x.Id, x => x.FullName);
        return new(rows.Select(x => Map(x, names.GetValueOrDefault(x.EmployeeId, "Employee"))).ToArray(), page.SafePage, page.SafePageSize, total);
    }

    public async Task<AttendanceCorrectionDto> SubmitAsync(RequestAttendanceCorrection r, CancellationToken ct)
    {
        r = r with { RequestedClockIn = r.RequestedClockIn.ToUniversalTime(), RequestedClockOut = r.RequestedClockOut.ToUniversalTime() };
        var employeeId = r.EmployeeId ?? user.EmployeeId ?? throw new UnauthorizedAccessException("A linked employee is required.");
        if (!user.HasPermission(Permissions.AttendanceManage) && (!user.HasPermission(Permissions.SelfService) || user.EmployeeId != employeeId))
            throw new UnauthorizedAccessException("You can only request corrections to your own attendance.");
        var person = await employees.GetByIdAsync(employeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        Required(r.Reason, "Correction reason");
        if (r.Reason.Trim().Length is < 10 or > 500) throw new DomainException("Explain the correction in 10 to 500 characters.");
        AttendanceCalendar.ValidateInterval(r.RequestedClockIn, r.RequestedClockOut);
        if (person.Status is EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned)
            throw new DomainException("Corrections cannot be submitted for an inactive employee.");
        if (r.WorkDate < person.HireDate) throw new DomainException("Attendance cannot precede the employee's hire date.");
        var policy = await policies.FirstOrDefaultAsync(_ => true, ct) ?? new AttendancePolicy();
        var company = await tenants.GetByIdAsync(TenantId, ct);
        var original = r.AttendanceRecordId.HasValue
            ? await records.GetByIdAsync(r.AttendanceRecordId.Value, ct) ?? throw new KeyNotFoundException("Attendance session not found.") : null;
        if (original is not null && (original.EmployeeId != employeeId || original.WorkDate != r.WorkDate))
            throw new DomainException("The selected session does not belong to this employee and workday.");
        if (AttendanceCalendar.WorkDate(r.RequestedClockIn, AttendanceCalendar.Zone(company?.TimeZone),
                original?.ScheduledStartAt ?? policy.OfficeStartsAt, original?.ScheduledEndAt ?? policy.OfficeEndsAt) != r.WorkDate)
            throw new DomainException("Check-in must fall on the selected workday in the company time zone.");
        if (await corrections.AnyAsync(x => x.EmployeeId == employeeId && x.WorkDate == r.WorkDate && x.Status == WorkflowStatus.Pending, ct))
            throw new DomainException("An attendance correction is already pending for this workday.");
        await EnsureNoOverlap(employeeId, original?.Id, r.RequestedClockIn, r.RequestedClockOut, ct);
        var row = new AttendanceCorrection { TenantId = TenantId, EmployeeId = employeeId, AttendanceRecordId = original?.Id,
            OriginalRecordVersion = original?.Version, WorkDate = r.WorkDate, RequestedClockIn = r.RequestedClockIn,
            RequestedClockOut = r.RequestedClockOut, Reason = r.Reason.Trim() };
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await corrections.AddAsync(row, ct);
        await notifications.QueueForPermissionAsync(Permissions.AttendanceManage, "Attendance correction requested",
            $"{person.FullName} requested a correction for {r.WorkDate:dd MMM yyyy}.", "attendance", "/attendance", ct);
        await notifications.QueueForEmployeesAsync([person.ManagerId], "Attendance correction requested",
            $"{person.FullName} requested a correction for {r.WorkDate:dd MMM yyyy}.", "attendance", "/my-team", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row, person.FullName);
    }

    public async Task<AttendanceCorrectionDto> ReviewAsync(Guid id, ReviewAttendanceCorrection r, CancellationToken ct)
    {
        var row = await corrections.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Correction not found.");
        var person = await employees.GetByIdAsync(row.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        if (user.EmployeeId == row.EmployeeId || row.CreatedBy == user.UserId)
            throw new UnauthorizedAccessException("A different reviewer must approve or reject this correction.");
        if (!user.HasPermission(Permissions.AttendanceManage)
            && !(user.HasPermission(Permissions.TeamApprove) && user.EmployeeId.HasValue && person.ManagerId == user.EmployeeId))
            throw new UnauthorizedAccessException("You can only review corrections for your direct reports.");
        CheckVersion(row, r.Version);
        if (row.Status != WorkflowStatus.Pending) throw new DomainException("Only pending corrections can be reviewed.");
        if (!r.Approve && string.IsNullOrWhiteSpace(r.Comment)) throw new DomainException("A rejection reason is required.");
        if (r.Comment?.Length > 500) throw new DomainException("Review comments cannot exceed 500 characters.");
        if (r.Approve)
        {
            await EnsureNoOverlap(row.EmployeeId, row.AttendanceRecordId, row.RequestedClockIn, row.RequestedClockOut, ct);
            var policy = await policies.FirstOrDefaultAsync(_ => true, ct) ?? new AttendancePolicy();
            AttendanceRecord record;
            if (row.AttendanceRecordId.HasValue)
            {
                record = await records.GetByIdAsync(row.AttendanceRecordId.Value, ct) ?? throw new DomainException("The original session no longer exists.");
                CheckVersion(record, row.OriginalRecordVersion ?? 0);
            }
            else
            {
                record = new AttendanceRecord { TenantId = TenantId, EmployeeId = row.EmployeeId, WorkDate = row.WorkDate,
                    ScheduledStartAt = policy.OfficeStartsAt, ScheduledEndAt = policy.OfficeEndsAt, RequiredMinutes = policy.RequiredMinutesPerDay,
                    LateGraceMinutes = policy.LateGraceMinutes, EarlyDepartureGraceMinutes = policy.EarlyDepartureGraceMinutes };
                await records.AddAsync(record, ct);
                row.AttendanceRecordId = record.Id;
            }
            record.ClockedInAt = row.RequestedClockIn; record.ClockedOutAt = row.RequestedClockOut;
            record.WorkHours = Math.Round((decimal)(row.RequestedClockOut - row.RequestedClockIn).TotalHours, 2);
            record.Source = "correction"; record.Notes = row.Reason;
            // Original captured evidence stays in the audit trail, not on corrected events.
            record.ClockInLatitude = record.ClockInLongitude = record.ClockInAccuracyMeters = null;
            record.ClockOutLatitude = record.ClockOutLongitude = record.ClockOutAccuracyMeters = null;
            record.ClockInAddress = record.ClockOutAddress = record.ClockInIpAddress = record.ClockOutIpAddress = null;
            record.ClockInUserAgent = record.ClockOutUserAgent = null;
            var day = (await records.ListAsync(x => x.EmployeeId == row.EmployeeId && x.WorkDate == row.WorkDate && x.Id != record.Id, cancellationToken: ct))
                .Append(record).OrderBy(x => x.ClockedInAt).ToArray();
            var isWorkingDay = policy.WorkingDaysCsv.Split(',').Any(x => x.Trim().Equals(row.WorkDate.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase));
            var exempt = !isWorkingDay || await holidays.AnyAsync(x => x.Date == row.WorkDate && !x.IsOptional && (!x.LocationId.HasValue || x.LocationId == person.LocationId), ct)
                || await leave.AnyAsync(x => x.EmployeeId == row.EmployeeId && x.Status == LeaveRequestStatus.Approved && x.StartsOn <= row.WorkDate && x.EndsOn >= row.WorkDate, ct);
            var required = exempt ? 0 : (day[0].RequiredMinutes ?? policy.RequiredMinutesPerDay) / 60m;
            decimal accumulated = 0;
            foreach (var session in day)
            {
                session.OvertimeHours = Math.Round(Math.Max(0, accumulated + session.WorkHours - required) - Math.Max(0, accumulated - required), 2);
                accumulated += session.WorkHours;
            }
            person.UpdatedAt = DateTimeOffset.UtcNow;
        }
        row.Status = r.Approve ? WorkflowStatus.Approved : WorkflowStatus.Rejected;
        row.ReviewedBy = user.UserId; row.ReviewedAt = DateTimeOffset.UtcNow; row.ReviewComment = r.Comment?.Trim();
        await notifications.QueueForEmployeesAsync([row.EmployeeId], $"Attendance correction {row.Status.ToString().ToLowerInvariant()}",
            $"Your correction for {row.WorkDate:dd MMM yyyy} was reviewed.", "attendance", "/my-services", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row, person.FullName);
    }

    public async Task CancelAsync(Guid id, long version, CancellationToken ct)
    {
        var row = await corrections.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Correction not found.");
        if (!user.HasPermission(Permissions.SelfService) || user.EmployeeId != row.EmployeeId) throw new UnauthorizedAccessException("You can only cancel your own correction.");
        CheckVersion(row, version);
        if (row.Status != WorkflowStatus.Pending) throw new DomainException("Only pending corrections can be cancelled.");
        row.Status = WorkflowStatus.Cancelled;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureNoOverlap(Guid employeeId, Guid? excludedId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (await records.AnyAsync(x => x.EmployeeId == employeeId && x.Id != excludedId && x.ClockedInAt < end && (x.ClockedOutAt == null || x.ClockedOutAt > start), ct))
            throw new DomainException("The correction overlaps another session. Select the existing session when correcting a missed check-out.");
    }
    private static AttendanceCorrectionDto Map(AttendanceCorrection x, string name) => new(x.Id, x.EmployeeId, name, x.AttendanceRecordId,
        x.WorkDate, x.RequestedClockIn, x.RequestedClockOut, x.Reason, x.Status, x.ReviewComment, x.CreatedAt, x.ReviewedAt, x.Version);
}
