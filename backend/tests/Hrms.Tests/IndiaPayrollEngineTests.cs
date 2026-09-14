using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class IndiaPayrollEngineTests
{
    private static readonly CancellationToken Ct = default;

    [Fact]
    public async Task Calculates_salary_loss_of_pay_overtime_and_enrolled_contributions_from_source_records()
    {
        await using var h = new Harness();
        var e = h.Employee();
        var run = h.Run();
        h.Db.PayrollPolicies.Add(new PayrollPolicy { TenantId = h.TenantId, MissingAttendance = "ignore", OvertimeMultiplier = 1.5m });
        h.Db.EmployeePayrollProfiles.Add(new EmployeePayrollProfile { TenantId = h.TenantId, EmployeeId = e.Id,
            PfEnabled = true, EsiEnabled = true, MonthlyTds = 1000m, ProfessionalTax = 200m });
        var unpaid = new LeaveType { TenantId = h.TenantId, Name = "Unpaid", Code = "LOP", IsPaid = false };
        h.Db.LeaveTypes.Add(unpaid);
        h.Db.LeaveRequests.Add(new LeaveRequest { TenantId = h.TenantId, EmployeeId = e.Id, LeaveTypeId = unpaid.Id,
            StartsOn = new(2026, 9, 10), EndsOn = new(2026, 9, 11), Days = 2m, Status = LeaveRequestStatus.Approved });
        h.Db.AttendanceRecords.AddRange(
            new AttendanceRecord { TenantId = h.TenantId, EmployeeId = e.Id, WorkDate = new(2026, 9, 14), Status = AttendanceStatus.Absent },
            new AttendanceRecord { TenantId = h.TenantId, EmployeeId = e.Id, WorkDate = new(2026, 9, 15), Status = AttendanceStatus.HalfDay },
            new AttendanceRecord { TenantId = h.TenantId, EmployeeId = e.Id, WorkDate = new(2026, 9, 16), Status = AttendanceStatus.Present, OvertimeHours = 4m });
        await h.Db.SaveChangesAsync(Ct);

        await h.Engine.CalculateAsync(run, Ct);
        await h.Db.SaveChangesAsync(Ct);

        var item = Assert.Single(await h.Db.PayrollItems.ToListAsync(Ct));
        Assert.Equal(15000m, item.BasicPay);
        Assert.Equal(15000m, item.Allowances);
        Assert.Equal(750m, item.OvertimePay);
        Assert.Equal(30750m, item.GrossPay);
        Assert.Equal(5494.38m, item.Deductions);
        Assert.Equal(1000m, item.Taxes);
        Assert.Equal(24255.62m, item.NetPay);
        Assert.Contains("\"lostDays\":3.5", item.BreakdownJson);
        Assert.Equal(item.NetPay, run.NetTotal);
    }

    [Fact]
    public async Task Blocks_calculation_without_employee_profile_and_resets_unapproved_items_when_policy_changes()
    {
        await using var h = new Harness();
        var employee = h.Employee();
        var run = h.Run();
        h.Db.PayrollPolicies.Add(new PayrollPolicy { TenantId = h.TenantId, MissingAttendance = "ignore" });
        await h.Db.SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Engine.CalculateAsync(run, Ct));

        h.Db.EmployeePayrollProfiles.Add(new EmployeePayrollProfile { TenantId = h.TenantId, EmployeeId = employee.Id });
        await h.Db.SaveChangesAsync(Ct);
        await h.Engine.CalculateAsync(run, Ct);
        run.Status = PayrollRunStatus.Processing;
        await h.Db.SaveChangesAsync(Ct);
        Assert.Single(await h.Db.PayrollItems.ToListAsync(Ct));

        var p = await h.Engine.GetPolicyAsync(Ct);
        await h.Engine.SavePolicyAsync(new("annual", "fixed30", "ignore", true, true, true, 8m,
            1m, .12m, .12m, 15000m, .0075m, .0325m, 21000m, true, false, p.Version), Ct);
        Assert.Equal(PayrollRunStatus.Draft, run.Status);
        Assert.Empty(await h.Db.PayrollItems.ToListAsync(Ct));
        Assert.Equal(0, run.NetTotal);
    }

    [Fact]
    public async Task Working_day_policy_counts_unpaid_leave_across_a_weekend_once_per_scheduled_day()
    {
        await using var h = new Harness();
        var employee = h.Employee();
        var run = h.Run();
        h.Db.PayrollPolicies.Add(new PayrollPolicy { TenantId = h.TenantId, PayableDaysBasis = "working", MissingAttendance = "ignore" });
        h.Db.EmployeePayrollProfiles.Add(new EmployeePayrollProfile { TenantId = h.TenantId, EmployeeId = employee.Id });
        var leaveType = new LeaveType { TenantId = h.TenantId, Name = "Unpaid", Code = "LOP", IsPaid = false };
        h.Db.LeaveTypes.Add(leaveType);
        h.Db.LeaveRequests.Add(new LeaveRequest { TenantId = h.TenantId, EmployeeId = employee.Id, LeaveTypeId = leaveType.Id,
            StartsOn = new(2026, 9, 11), EndsOn = new(2026, 9, 14), Days = 2m, Status = LeaveRequestStatus.Approved });
        await h.Db.SaveChangesAsync(Ct);

        await h.Engine.CalculateAsync(run, Ct);
        await h.Db.SaveChangesAsync(Ct);
        var item = Assert.Single(await h.Db.PayrollItems.ToListAsync(Ct));
        Assert.Contains("\"lostDays\":2", item.BreakdownJson);
        Assert.Equal(2727.27m, item.Deductions);
    }

    [Fact]
    public async Task Pays_employee_who_left_during_month_only_through_termination_date()
    {
        await using var h = new Harness();
        var employee = h.Employee();
        employee.Status = EmploymentStatus.Resigned;
        employee.TerminationDate = new(2026, 9, 15);
        var run = h.Run();
        h.Db.PayrollPolicies.Add(new PayrollPolicy { TenantId = h.TenantId, MissingAttendance = "ignore" });
        h.Db.EmployeePayrollProfiles.Add(new EmployeePayrollProfile { TenantId = h.TenantId, EmployeeId = employee.Id });
        await h.Db.SaveChangesAsync(Ct);

        await h.Engine.CalculateAsync(run, Ct);
        await h.Db.SaveChangesAsync(Ct);
        var item = Assert.Single(await h.Db.PayrollItems.ToListAsync(Ct));
        Assert.Equal(15000m, item.GrossPay);
        Assert.Equal(15000m, item.NetPay);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public CurrentTenant Tenant { get; } = new();
        public HrmsDbContext Db { get; }
        public IndiaPayrollEngine Engine { get; }
        public Harness()
        {
            Tenant.Set(TenantId);
            Db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Tenant, new TestCurrentUser(), new TestNotificationPublisher());
            Engine = new IndiaPayrollEngine(Db, Tenant);
        }
        public Employee Employee()
        {
            var row = new Employee { TenantId = TenantId, EmployeeNumber = "EMP001", FirstName = "Asha", LastName = "Rao",
                WorkEmail = "asha@example.test", HireDate = new(2026, 1, 1), BaseSalary = 360000m, SalaryCurrency = "INR" };
            Db.Employees.Add(row);
            return row;
        }
        public PayrollRun Run()
        {
            var row = new PayrollRun { TenantId = TenantId, Name = "September 2026", PeriodStart = new(2026, 9, 1),
                PeriodEnd = new(2026, 9, 30), PaymentDate = new(2026, 10, 1), Currency = "INR" };
            Db.PayrollRuns.Add(row);
            return row;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
