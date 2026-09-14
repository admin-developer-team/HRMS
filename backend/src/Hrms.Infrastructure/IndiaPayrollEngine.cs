using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure;

public sealed record PayrollPolicyUpdate(string SalaryBasis, string PayableDaysBasis, string MissingAttendance,
    bool DeductUnpaidLeave, bool DeductAbsences, bool DeductHalfDays, decimal StandardDailyHours,
    decimal OvertimeMultiplier, decimal PfEmployeeRate, decimal PfEmployerRate, decimal PfWageCeiling,
    decimal EsiEmployeeRate, decimal EsiEmployerRate, decimal EsiGrossCeiling,
    bool RequireStatutoryReview, bool ReleasePayslipsOnApproval, long Version);
public sealed record EmployeePayrollProfileUpdate(decimal BasicPercent, decimal HraPercentOfBasic,
    bool PfEnabled, bool EsiEnabled, decimal MonthlyTds, decimal ProfessionalTax,
    decimal OtherMonthlyDeduction, decimal? OvertimeHourlyRate, string TaxRegime, long Version);
public sealed record PayrollAdjustmentRequest(Guid EmployeeId, string Code, string Description, bool IsEarning, decimal Amount);

public sealed class IndiaPayrollEngine(HrmsDbContext db, ICurrentTenant tenant) : IPayrollCalculationEngine
{
    private Guid TenantId => tenant.TenantId ?? throw new UnauthorizedAccessException();
    private static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public async Task<bool> RequiresReviewAsync(CancellationToken ct) =>
        (await db.PayrollPolicies.SingleOrDefaultAsync(ct))?.RequireStatutoryReview ?? true;

    public async Task<PayrollPolicy> GetPolicyAsync(CancellationToken ct) =>
        await db.PayrollPolicies.SingleOrDefaultAsync(ct) ?? new PayrollPolicy { TenantId = TenantId };

    public async Task<PayrollPolicy> SavePolicyAsync(PayrollPolicyUpdate request, CancellationToken ct)
    {
        if (request.SalaryBasis is not ("annual" or "monthly") || request.PayableDaysBasis is not ("calendar" or "fixed30" or "working")
            || request.MissingAttendance is not ("block" or "ignore" or "unpaid"))
            throw new DomainException("Choose supported salary, payable-day and attendance policies.");
        if (request.StandardDailyHours is <= 0 or > 16 || request.OvertimeMultiplier is < 0 or > 5
            || request.PfEmployeeRate is < 0 or > 0.25m || request.PfEmployerRate is < 0 or > 0.25m
            || request.EsiEmployeeRate is < 0 or > 0.10m || request.EsiEmployerRate is < 0 or > 0.10m
            || request.PfWageCeiling < 0 || request.EsiGrossCeiling < 0)
            throw new DomainException("Payroll rates or working hours are outside supported ranges.");
        var policy = await db.PayrollPolicies.SingleOrDefaultAsync(ct);
        if (policy is null)
        {
            if (request.Version != 0) throw new DomainException("Reload payroll policy before saving.");
            policy = new PayrollPolicy { TenantId = TenantId };
            db.PayrollPolicies.Add(policy);
        }
        else if (policy.Version != request.Version) throw new DomainException("Payroll policy changed. Reload and retry.");
        policy.SalaryBasis = request.SalaryBasis; policy.PayableDaysBasis = request.PayableDaysBasis;
        policy.MissingAttendance = request.MissingAttendance; policy.DeductUnpaidLeave = request.DeductUnpaidLeave;
        policy.DeductAbsences = request.DeductAbsences; policy.DeductHalfDays = request.DeductHalfDays;
        policy.StandardDailyHours = request.StandardDailyHours; policy.OvertimeMultiplier = request.OvertimeMultiplier;
        policy.PfEmployeeRate = request.PfEmployeeRate; policy.PfEmployerRate = request.PfEmployerRate;
        policy.PfWageCeiling = request.PfWageCeiling; policy.EsiEmployeeRate = request.EsiEmployeeRate;
        policy.EsiEmployerRate = request.EsiEmployerRate; policy.EsiGrossCeiling = request.EsiGrossCeiling;
        policy.RequireStatutoryReview = request.RequireStatutoryReview;
        policy.ReleasePayslipsOnApproval = request.ReleasePayslipsOnApproval;
        await InvalidateProcessingRunsAsync(ct);
        await db.SaveChangesAsync(ct);
        return policy;
    }

    public async Task<EmployeePayrollProfile> GetProfileAsync(Guid employeeId, CancellationToken ct)
    {
        _ = await db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId, ct)
            ?? throw new KeyNotFoundException("Employee not found.");
        return await db.EmployeePayrollProfiles.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, ct)
            ?? new EmployeePayrollProfile { TenantId = TenantId, EmployeeId = employeeId };
    }

    public async Task<EmployeePayrollProfile> SaveProfileAsync(Guid employeeId, EmployeePayrollProfileUpdate request, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId, ct)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.SalaryCurrency != "INR") throw new DomainException("India payroll profiles require an INR salary.");
        if (request.BasicPercent is < 0 or > 100 || request.HraPercentOfBasic is < 0 or > 100
            || request.BasicPercent * (1 + request.HraPercentOfBasic / 100m) > 100m
            || request.MonthlyTds < 0 || request.ProfessionalTax < 0 || request.OtherMonthlyDeduction < 0
            || request.OvertimeHourlyRate < 0 || request.TaxRegime is not ("new" or "old"))
            throw new DomainException("Enter a valid salary structure and non-negative deductions.");
        var profile = await db.EmployeePayrollProfiles.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
        if (profile is null)
        {
            if (request.Version != 0) throw new DomainException("Reload employee payroll profile before saving.");
            profile = new EmployeePayrollProfile { TenantId = TenantId, EmployeeId = employeeId };
            db.EmployeePayrollProfiles.Add(profile);
        }
        else if (profile.Version != request.Version) throw new DomainException("Payroll profile changed. Reload and retry.");
        profile.BasicPercent = request.BasicPercent; profile.HraPercentOfBasic = request.HraPercentOfBasic;
        profile.PfEnabled = request.PfEnabled; profile.EsiEnabled = request.EsiEnabled;
        profile.MonthlyTds = Money(request.MonthlyTds); profile.ProfessionalTax = Money(request.ProfessionalTax);
        profile.OtherMonthlyDeduction = Money(request.OtherMonthlyDeduction);
        profile.OvertimeHourlyRate = request.OvertimeHourlyRate.HasValue ? Money(request.OvertimeHourlyRate.Value) : null;
        profile.TaxRegime = request.TaxRegime;
        await InvalidateProcessingRunsAsync(ct);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<IReadOnlyList<PayrollAdjustment>> ListAdjustmentsAsync(Guid runId, CancellationToken ct)
    {
        _ = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == runId, ct) ?? throw new KeyNotFoundException("Payroll run not found.");
        return await db.PayrollAdjustments.Where(x => x.PayrollRunId == runId).OrderBy(x => x.EmployeeId).ThenBy(x => x.Code).ToListAsync(ct);
    }

    public async Task<PayrollAdjustment> AddAdjustmentAsync(Guid runId, PayrollAdjustmentRequest request, CancellationToken ct)
    {
        var run = await EditableRunAsync(runId, ct);
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == request.EmployeeId, ct)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.HireDate > run.PeriodEnd || (employee.TerminationDate.HasValue && employee.TerminationDate.Value < run.PeriodStart)
            || employee.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended)
            throw new DomainException("This employee is not eligible for the selected payroll period.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 30 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            || request.Amount is <= 0 or > 10000000 || request.Description.Trim().Length is < 3 or > 200)
            throw new DomainException("Enter a valid adjustment code, amount and description.");
        if (await db.PayrollAdjustments.AnyAsync(x => x.PayrollRunId == runId && x.EmployeeId == request.EmployeeId && x.Code == code, ct))
            throw new DomainException("This employee already has an adjustment with that code in this run.");
        var row = new PayrollAdjustment { TenantId = TenantId, PayrollRunId = runId, EmployeeId = request.EmployeeId,
            Code = code, Description = request.Description.Trim(), IsEarning = request.IsEarning, Amount = Money(request.Amount) };
        db.PayrollAdjustments.Add(row);
        await InvalidateRunAsync(run, ct);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task DeleteAdjustmentAsync(Guid runId, Guid adjustmentId, CancellationToken ct)
    {
        var run = await EditableRunAsync(runId, ct);
        var row = await db.PayrollAdjustments.FirstOrDefaultAsync(x => x.Id == adjustmentId && x.PayrollRunId == runId, ct)
            ?? throw new KeyNotFoundException("Adjustment not found.");
        db.PayrollAdjustments.Remove(row);
        await InvalidateRunAsync(run, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task CalculateAsync(PayrollRun run, CancellationToken ct)
    {
        if (run.Currency != "INR") throw new DomainException("India payroll must use INR.");
        var policy = await db.PayrollPolicies.SingleOrDefaultAsync(ct)
            ?? throw new DomainException("Save and review the company payroll policy before calculation.");
        var workforce = await db.Employees.AsNoTracking().Where(x =>
            (x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod
                || ((x.Status == EmploymentStatus.Terminated || x.Status == EmploymentStatus.Resigned)
                    && x.TerminationDate.HasValue && x.TerminationDate.Value >= run.PeriodStart))
            && x.HireDate <= run.PeriodEnd).OrderBy(x => x.EmployeeNumber).ToListAsync(ct);
        if (workforce.Count == 0) throw new DomainException("No eligible employees are available for this payroll period.");
        if (workforce.Any(x => x.SalaryCurrency != "INR")) throw new DomainException("All payroll employees must have an INR salary.");
        var employeeIds = workforce.Select(x => x.Id).ToArray();
        var profiles = await db.EmployeePayrollProfiles.AsNoTracking().Where(x => employeeIds.Contains(x.EmployeeId)).ToDictionaryAsync(x => x.EmployeeId, ct);
        var missingProfile = workforce.FirstOrDefault(x => !profiles.ContainsKey(x.Id));
        if (missingProfile is not null) throw new DomainException($"Set and review the payroll profile for {missingProfile.FullName} before calculation.");
        var attendance = await db.AttendanceRecords.AsNoTracking().Where(x => employeeIds.Contains(x.EmployeeId)
            && x.WorkDate >= run.PeriodStart && x.WorkDate <= run.PeriodEnd).ToListAsync(ct);
        var leaves = await db.LeaveRequests.AsNoTracking().Where(x => employeeIds.Contains(x.EmployeeId)
            && x.StartsOn <= run.PeriodEnd && x.EndsOn >= run.PeriodStart
            && (x.Status == LeaveRequestStatus.Pending || x.Status == LeaveRequestStatus.Approved)).ToListAsync(ct);
        var pendingLeave = leaves.FirstOrDefault(x => x.Status == LeaveRequestStatus.Pending);
        if (pendingLeave is not null) throw new DomainException("Resolve pending leave requests in this period before payroll calculation.");
        if (await db.AttendanceCorrections.AnyAsync(x => employeeIds.Contains(x.EmployeeId)
            && x.WorkDate >= run.PeriodStart && x.WorkDate <= run.PeriodEnd && x.Status == WorkflowStatus.Pending, ct))
            throw new DomainException("Resolve pending attendance corrections before payroll calculation.");
        var leaveTypeIds = leaves.Select(x => x.LeaveTypeId).Distinct().ToArray();
        var leaveTypes = await db.LeaveTypes.AsNoTracking().Where(x => leaveTypeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var attendancePolicy = await db.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(ct);
        var workingDays = (attendancePolicy?.WorkingDaysCsv ?? "Monday,Tuesday,Wednesday,Thursday,Friday")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (workingDays.Count == 0) throw new DomainException("Configure working days in the attendance policy before payroll.");
        var holidays = await db.Holidays.AsNoTracking().Where(x => x.Date >= run.PeriodStart && x.Date <= run.PeriodEnd).ToListAsync(ct);
        var optionalHolidayIds = holidays.Where(x => x.IsOptional).Select(x => x.Id).ToArray();
        var selectedHolidays = (await db.HolidaySelections.AsNoTracking().Where(x => employeeIds.Contains(x.EmployeeId)
            && optionalHolidayIds.Contains(x.HolidayId)).ToListAsync(ct)).Select(x => (x.EmployeeId, x.HolidayId)).ToHashSet();
        var adjustments = await db.PayrollAdjustments.AsNoTracking().Where(x => x.PayrollRunId == run.Id).ToListAsync(ct);
        var existing = await db.PayrollItems.Where(x => x.PayrollRunId == run.Id).ToDictionaryAsync(x => x.EmployeeId, ct);
        var daysInMonth = run.PeriodEnd.DayNumber - run.PeriodStart.DayNumber + 1;
        var allDates = Enumerable.Range(0, daysInMonth).Select(run.PeriodStart.AddDays).ToArray();
        decimal grossTotal = 0, deductionTotal = 0, netTotal = 0;
        foreach (var employee in workforce)
        {
            var profile = profiles[employee.Id];
            var employeeDates = allDates.Where(x => x >= employee.HireDate && (!employee.TerminationDate.HasValue || x <= employee.TerminationDate.Value)).ToArray();
            var employeeHolidays = holidays.Where(x => (x.LocationId is null || x.LocationId == employee.LocationId)
                && (!x.IsOptional || selectedHolidays.Contains((employee.Id, x.Id)))).Select(x => x.Date).ToHashSet();
            var eligibleDates = policy.PayableDaysBasis == "working"
                ? employeeDates.Where(x => workingDays.Contains(x.DayOfWeek.ToString()) && !employeeHolidays.Contains(x)).ToArray()
                : employeeDates;
            var divisor = policy.PayableDaysBasis switch { "fixed30" => 30m, "working" => allDates.Count(x =>
                workingDays.Contains(x.DayOfWeek.ToString()) && !employeeHolidays.Contains(x)), _ => daysInMonth };
            if (divisor <= 0) throw new DomainException("Payroll has no payable days under the selected policy.");
            var unpaidByDate = new Dictionary<DateOnly, decimal>();
            foreach (var leave in leaves.Where(x => x.EmployeeId == employee.Id && x.Status == LeaveRequestStatus.Approved))
            {
                if (!leaveTypes.TryGetValue(leave.LeaveTypeId, out var type)) throw new DomainException("A leave type used by payroll is missing.");
                if (type.IsPaid || !policy.DeductUnpaidLeave) continue;
                var perDay = leave.StartsOn == leave.EndsOn && leave.Days == .5m ? .5m : 1m;
                foreach (var date in eligibleDates.Where(x => x >= leave.StartsOn && x <= leave.EndsOn
                    && workingDays.Contains(x.DayOfWeek.ToString()) && !employeeHolidays.Contains(x)))
                    unpaidByDate[date] = Math.Min(1m, unpaidByDate.GetValueOrDefault(date) + perDay);
            }
            var employeeAttendance = attendance.Where(x => x.EmployeeId == employee.Id).ToArray();
            var byDate = employeeAttendance.GroupBy(x => x.WorkDate).ToDictionary(x => x.Key, x => x.ToArray());
            decimal lostDays = unpaidByDate.Values.Sum();
            foreach (var date in eligibleDates)
            {
                if (unpaidByDate.ContainsKey(date) || employeeHolidays.Contains(date)) continue;
                if (byDate.TryGetValue(date, out var sessions))
                {
                    if (policy.DeductAbsences && sessions.All(x => x.Status == AttendanceStatus.Absent)) lostDays += 1m;
                    else if (policy.DeductHalfDays && sessions.All(x => x.Status == AttendanceStatus.HalfDay)) lostDays += 0.5m;
                }
                else if (workingDays.Contains(date.DayOfWeek.ToString()))
                {
                    var coveredByPaidLeave = leaves.Any(x => x.EmployeeId == employee.Id && x.Status == LeaveRequestStatus.Approved
                        && x.StartsOn <= date && x.EndsOn >= date && leaveTypes[x.LeaveTypeId].IsPaid);
                    if (coveredByPaidLeave) continue;
                    if (policy.MissingAttendance == "block")
                        throw new DomainException($"Attendance for {employee.FullName} on {date:yyyy-MM-dd} is missing. Resolve it or change the company policy.");
                    if (policy.MissingAttendance == "unpaid") lostDays += 1m;
                }
            }
            var hireFraction = policy.PayableDaysBasis == "fixed30" ? Math.Min(1m, employeeDates.Length / (decimal)daysInMonth)
                : eligibleDates.Length / divisor;
            var monthlySalary = Money(policy.SalaryBasis == "annual" ? employee.BaseSalary / 12m : employee.BaseSalary);
            var periodSalary = Money(monthlySalary * hireFraction);
            var basic = Money(periodSalary * profile.BasicPercent / 100m);
            var hra = Money(basic * profile.HraPercentOfBasic / 100m);
            var special = periodSalary - basic - hra;
            var lop = Money(monthlySalary * Math.Min(lostDays, divisor) / divisor);
            var earnedBasic = Money(basic * Math.Max(0m, 1m - Math.Min(lostDays, divisor) / divisor));
            var overtimeHours = employeeAttendance.Sum(x => x.OvertimeHours);
            var overtimeRate = profile.OvertimeHourlyRate ?? Money(monthlySalary / divisor / policy.StandardDailyHours * policy.OvertimeMultiplier);
            var overtime = Money(overtimeHours * overtimeRate);
            var extras = adjustments.Where(x => x.EmployeeId == employee.Id).ToArray();
            var earnings = extras.Where(x => x.IsEarning).Sum(x => x.Amount);
            var extraDeductions = extras.Where(x => !x.IsEarning).Sum(x => x.Amount);
            var pfBase = policy.PfWageCeiling > 0 ? Math.Min(earnedBasic, policy.PfWageCeiling) : earnedBasic;
            var pfEmployee = profile.PfEnabled ? Money(pfBase * policy.PfEmployeeRate) : 0m;
            var pfEmployer = profile.PfEnabled ? Money(pfBase * policy.PfEmployerRate) : 0m;
            var esiBase = Math.Max(0m, periodSalary - lop + overtime + earnings);
            var esiEmployee = profile.EsiEnabled ? Money(esiBase * policy.EsiEmployeeRate) : 0m;
            var esiEmployer = profile.EsiEnabled ? Money(esiBase * policy.EsiEmployerRate) : 0m;
            var gross = periodSalary + overtime + earnings;
            var deductions = lop + pfEmployee + esiEmployee + profile.ProfessionalTax + profile.OtherMonthlyDeduction + extraDeductions;
            var taxes = profile.MonthlyTds;
            var net = gross - deductions - taxes;
            if (net < 0) throw new DomainException($"Net pay for {employee.FullName} is negative. Review deductions and adjustments.");
            if (!existing.TryGetValue(employee.Id, out var item))
            {
                item = new PayrollItem { TenantId = TenantId, PayrollRunId = run.Id, EmployeeId = employee.Id };
                db.PayrollItems.Add(item);
            }
            item.BasicPay = basic; item.Allowances = hra + special + earnings; item.OvertimePay = overtime;
            item.Deductions = Money(deductions); item.Taxes = Money(taxes); item.GrossPay = Money(gross); item.NetPay = Money(net);
            item.BreakdownJson = JsonSerializer.Serialize(new {
                salaryBasis = policy.SalaryBasis, monthlySalary, payableDaysBasis = policy.PayableDaysBasis, divisor,
                hireFraction, lostDays, basic, hra, specialAllowance = special, lossOfPay = lop,
                overtimeHours, overtimeRate, overtime, pfEmployee, pfEmployer, esiEmployee, esiEmployer,
                professionalTax = profile.ProfessionalTax, tds = taxes, otherDeduction = profile.OtherMonthlyDeduction,
                taxRegime = profile.TaxRegime, adjustments = extras.Select(x => new { x.Code, x.Description, x.IsEarning, x.Amount }),
                attendanceIds = employeeAttendance.Select(x => x.Id), leaveIds = leaves.Where(x => x.EmployeeId == employee.Id).Select(x => x.Id)
            });
            grossTotal += item.GrossPay; deductionTotal += item.Deductions + item.Taxes; netTotal += item.NetPay;
        }
        foreach (var item in existing.Values.Where(x => !employeeIds.Contains(x.EmployeeId))) db.PayrollItems.Remove(item);
        run.GrossTotal = Money(grossTotal); run.DeductionTotal = Money(deductionTotal); run.NetTotal = Money(netTotal);
        run.PolicySnapshotJson = JsonSerializer.Serialize(policy);
    }

    private async Task<PayrollRun> EditableRunAsync(Guid id, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Payroll run not found.");
        if (run.Status is not (PayrollRunStatus.Draft or PayrollRunStatus.Processing))
            throw new DomainException("Approved or paid payroll cannot be changed.");
        return run;
    }

    private async Task InvalidateProcessingRunsAsync(CancellationToken ct)
    {
        var runs = await db.PayrollRuns.Where(x => x.Status == PayrollRunStatus.Processing).ToListAsync(ct);
        foreach (var run in runs) await InvalidateRunAsync(run, ct);
    }

    private async Task InvalidateRunAsync(PayrollRun run, CancellationToken ct)
    {
        if (run.Status != PayrollRunStatus.Processing) return;
        db.PayrollItems.RemoveRange(await db.PayrollItems.Where(x => x.PayrollRunId == run.Id).ToListAsync(ct));
        run.Status = PayrollRunStatus.Draft; run.CalculatedAt = null; run.StatutoryReviewedAt = null;
        run.StatutoryReviewedBy = null; run.GrossTotal = 0; run.DeductionTotal = 0; run.NetTotal = 0;
        run.PolicySnapshotJson = null;
    }
}
