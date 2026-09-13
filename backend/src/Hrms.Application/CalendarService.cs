using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record CalendarObservance(DateOnly Date, string Name, bool IsPublicHoliday);
public sealed record CalendarLocation(Guid Id, string Name);
public sealed record CalendarHoliday(Guid Id, string Name, DateOnly Date, Guid? LocationId, string? LocationName, bool IsOptional, bool AppliesToMe, bool Selected, long Version);
public sealed record CalendarLeave(DateOnly StartsOn, DateOnly EndsOn, string Status);
public sealed record CalendarAttendance(DateOnly Date, string Status, decimal WorkHours);
public sealed record CalendarMonth(int Year, int Month, string CountryCode, string? LocationName, IReadOnlyList<CalendarLocation> Locations, IReadOnlyList<string> WorkingDays,
    IReadOnlyList<CalendarHoliday> Holidays, IReadOnlyList<CalendarObservance> Observances,
    IReadOnlyList<CalendarLeave> Leave, IReadOnlyList<CalendarAttendance> Attendance, bool SuggestionsAvailable);
public sealed record SelectHolidayRequest(bool Selected);

public interface IPublicHolidaySource
{
    Task<(IReadOnlyList<CalendarObservance> Items, bool Available)> GetAsync(string countryCode, int year, CancellationToken ct);
}

public sealed class CalendarService(
    IRepository<Holiday> holidays, IRepository<HolidaySelection> selections, IRepository<Employee> employees,
    IRepository<Location> locations, IRepository<LeaveRequest> leaveRequests, IRepository<AttendanceRecord> attendance,
    IRepository<AttendancePolicy> policies, IRepository<Tenant> tenants, ICurrentTenant tenant, ICurrentUser user,
    IUnitOfWork unitOfWork, IPublicHolidaySource publicHolidays) : ServiceBase(tenant)
{
    public async Task<CalendarMonth> GetAsync(int year, int month, Guid? locationId, CancellationToken ct)
    {
        if (year < 2020 || year > 2100 || month < 1 || month > 12) throw new DomainException("Invalid calendar month.");
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var employee = user.EmployeeId.HasValue ? await employees.GetByIdAsync(user.EmployeeId.Value, ct) : null;
        var locationRows = await locations.ListAsync(cancellationToken: ct);
        var canManage = user.HasPermission(Permissions.WorkforceManage);
        var effectiveLocationId = canManage ? locationId : employee?.LocationId;
        var location = locationRows.FirstOrDefault(x => x.Id == effectiveLocationId);
        if (locationId.HasValue && canManage && location is null) throw new KeyNotFoundException("Location not found.");
        var countryCode = (location?.CountryCode ?? locationRows.FirstOrDefault(x => x.IsActive && !string.IsNullOrWhiteSpace(x.CountryCode))?.CountryCode ?? "IN").Trim().ToUpperInvariant();
        var holidayRows = await holidays.ListAsync(x => x.Date >= start && x.Date <= end && (!effectiveLocationId.HasValue || !x.LocationId.HasValue || x.LocationId == effectiveLocationId), q => q.OrderBy(x => x.Date), cancellationToken: ct);
        var selectedIds = employee is null ? new HashSet<Guid>() : (await selections.ListAsync(x => x.EmployeeId == employee.Id, cancellationToken: ct)).Select(x => x.HolidayId).ToHashSet();
        var mapped = holidayRows.Select(x => new CalendarHoliday(x.Id, x.Name, x.Date, x.LocationId,
            locationRows.FirstOrDefault(l => l.Id == x.LocationId)?.Name, x.IsOptional,
            !x.LocationId.HasValue || (employee is not null && x.LocationId == employee.LocationId), selectedIds.Contains(x.Id), x.Version)).ToArray();
        CalendarLeave[] leave = employee is null ? [] : (await leaveRequests.ListAsync(x => x.EmployeeId == employee.Id && x.StartsOn <= end && x.EndsOn >= start && x.Status != LeaveRequestStatus.Rejected && x.Status != LeaveRequestStatus.Cancelled, cancellationToken: ct))
            .Select(x => new CalendarLeave(x.StartsOn, x.EndsOn, x.Status.ToString())).ToArray();
        CalendarAttendance[] attendanceRows = employee is null ? [] : (await attendance.ListAsync(x => x.EmployeeId == employee.Id && x.WorkDate >= start && x.WorkDate <= end, cancellationToken: ct))
            .GroupBy(x => x.WorkDate).Select(g => new CalendarAttendance(g.Key, g.Any(x => x.ClockedOutAt is null) ? "In progress" : "Present", g.Sum(x => x.WorkHours))).ToArray();
        var policy = await policies.FirstOrDefaultAsync(_ => true, ct);
        var (observances, available) = await publicHolidays.GetAsync(countryCode, year, ct);
        return new CalendarMonth(year, month, countryCode, location?.Name,
            canManage ? locationRows.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new CalendarLocation(x.Id, x.Name)).ToArray() : [],
            (policy?.WorkingDaysCsv ?? "Monday,Tuesday,Wednesday,Thursday,Friday").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            mapped, observances.Where(x => x.Date >= start && x.Date <= end).ToArray(), leave, attendanceRows, available);
    }

    public async Task SelectAsync(Guid holidayId, bool selected, CancellationToken ct)
    {
        if (!user.HasPermission(Permissions.SelfService)) throw new UnauthorizedAccessException("Self-service access is required.");
        var employeeId = user.EmployeeId ?? throw new UnauthorizedAccessException("An employee profile is required.");
        var employee = await employees.GetByIdAsync(employeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        var holiday = await holidays.GetByIdAsync(holidayId, ct) ?? throw new KeyNotFoundException("Holiday not found.");
        if (!holiday.IsOptional) throw new DomainException("Only optional holidays can be selected.");
        if (holiday.LocationId.HasValue && holiday.LocationId != employee.LocationId) throw new UnauthorizedAccessException("Holiday does not apply to your location.");
        var company = await tenants.GetByIdAsync(TenantId, ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, AttendanceCalendar.Zone(company?.TimeZone)).DateTime);
        if (holiday.Date < today) throw new DomainException("Past holiday selections cannot be changed.");
        if (selected && await holidays.AnyAsync(x => x.Date == holiday.Date && !x.IsOptional && (!x.LocationId.HasValue || x.LocationId == employee.LocationId), ct))
            throw new DomainException("This date is already an office holiday.");
        var existing = await selections.FirstOrDefaultAsync(x => x.HolidayId == holidayId && x.EmployeeId == employeeId, ct);
        if (selected && existing is null)
        {
            if (await leaveRequests.AnyAsync(x => x.EmployeeId == employeeId && x.StartsOn <= holiday.Date && x.EndsOn >= holiday.Date && x.Status != LeaveRequestStatus.Cancelled && x.Status != LeaveRequestStatus.Rejected, ct))
                throw new DomainException("Cancel the overlapping leave request before selecting this holiday.");
            await selections.AddAsync(new HolidaySelection { TenantId = TenantId, HolidayId = holidayId, EmployeeId = employeeId }, ct);
        }
        else if (!selected && existing is not null) selections.Remove(existing);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
