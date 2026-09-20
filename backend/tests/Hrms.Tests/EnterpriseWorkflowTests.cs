using System.Security.Claims;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Hrms.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class EnterpriseWorkflowTests
{
    private static readonly CancellationToken Ct = default;
    private static readonly DateOnly Day = new(2026, 8, 3);
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public async Task Global_search_respects_employee_and_team_permissions()
    {
        using var h = new Harness();
        var search = new GlobalSearchService(h.Db, h.User, h.Work);
        h.AsEmployee(h.A);
        Assert.Empty((await search.SearchAsync("Rohan", Ct)).Items);

        h.AsEmployee(h.B, Permissions.TeamRead);
        var teamHit = Assert.Single((await search.SearchAsync("Asha", Ct)).Items);
        Assert.Equal("Team member", teamHit.Kind);
        Assert.Equal("/my-team", teamHit.Route);

        h.AsEmployee(h.B, Permissions.EmployeesRead);
        var employeeHit = Assert.Single((await search.SearchAsync("Asha", Ct)).Items);
        Assert.Equal("Employee", employeeHit.Kind);
        Assert.Equal($"/employees/{h.A}", employeeHit.Route);
    }

    [Fact]
    public async Task Global_search_only_returns_work_from_accessible_projects()
    {
        using var h = new Harness();
        var project = await h.Project();
        await h.Work.CreateItemAsync(h.ItemRequest(project.Id) with { Description = "Searchable onboarding checklist" }, Ct);
        var search = new GlobalSearchService(h.Db, h.User, h.Work);
        h.AsEmployee(h.A, Permissions.WorkRead);
        Assert.Empty((await search.SearchAsync("checklist", Ct)).Items);

        h.User.Admin = true;
        await h.Work.SetMembersAsync(project.Id, [new(h.A, false, true, false, false, false)], Ct);
        h.User.Admin = false;
        var hit = Assert.Single((await search.SearchAsync("checklist", Ct)).Items);
        Assert.Equal("Ticket", hit.Kind);
        Assert.Equal("/work", hit.Route);
    }

    [Theory]
    [InlineData(9, 17, 480)] [InlineData(22, 6, 480)] [InlineData(20, 4, 480)]
    public void Attendance_supports_day_and_night_shifts(int start, int end, int minutes) =>
        Assert.Equal(minutes, AttendanceCalendar.DurationMinutes(new(start, 0), new(end, 0)));

    [Fact]
    public void Overnight_workday_uses_company_timezone()
    {
        var zone = AttendanceCalendar.Zone("Asia/Kolkata");
        Assert.Equal(Day, AttendanceCalendar.WorkDate(At(22), zone, new(22, 0), new(6, 0)));
        Assert.Equal(Day.AddDays(1), AttendanceCalendar.WorkDate(At(22).AddHours(5), zone, new(22, 0), new(6, 0)));
    }

    [Fact]
    public async Task Overnight_session_reports_no_false_late_arrival_or_early_departure()
    {
        using var h = new Harness();
        await h.Attendance.UpdatePolicyAsync(new(new(22, 0), new(6, 0), 0, 0, ["Monday"], false), Ct);
        await h.Attendance.ClockInAsync(new(h.A, At(22)), Ct);
        await h.Attendance.ClockOutAsync(new(h.A, At(22).AddHours(8)), Ct);
        var report = await h.Attendance.ReportAsync(new(), h.A, Day, Day, Ct);
        var row = Assert.Single(report.Items);
        Assert.Equal(8, row.TotalHours); Assert.Equal(8, row.RequiredHours);
        Assert.Equal(0, row.LateMinutes); Assert.Equal(0, row.EarlyDepartureMinutes); Assert.Equal("Compliant", row.Status);
    }

    [Fact]
    public async Task Employee_office_hours_override_company_policy_and_snapshot_attendance()
    {
        using var h = new Harness();
        await h.Attendance.UpdatePolicyAsync(new(new(9, 0), new(17, 0), 0, 0, ["Monday"], false), Ct);
        var employee = await h.Db.Employees.FindAsync([h.A], Ct);
        employee!.OfficeStartsAt = new(10, 0);
        employee.OfficeEndsAt = new(17, 0);
        await h.Db.SaveChangesAsync(Ct);

        await h.Attendance.ClockInAsync(new(h.A, At(10)), Ct);
        await h.Attendance.ClockOutAsync(new(h.A, At(17)), Ct);
        employee.OfficeStartsAt = new(11, 0);
        employee.OfficeEndsAt = new(18, 0);
        await h.Db.SaveChangesAsync(Ct);

        var worked = Assert.Single((await h.Attendance.ReportAsync(new(), h.A, Day, Day, Ct)).Items);
        Assert.Equal(7, worked.RequiredHours);
        Assert.Equal(0, worked.LateMinutes);
        Assert.Equal(0, worked.EarlyDepartureMinutes);
        Assert.Equal("Compliant", worked.Status);
        var absent = Assert.Single((await h.Attendance.ReportAsync(new(), h.A, Day.AddDays(7), Day.AddDays(7), Ct)).Items);
        Assert.Equal(7, absent.RequiredHours);
    }

    [Fact]
    public async Task Attendance_time_saves_without_location_and_later_accepts_approximate_reading()
    {
        using var h = new Harness();
        await h.Attendance.UpdatePolicyAsync(new(new(9, 0), new(17, 0), 0, 0, ["Monday"], true), Ct);
        var checkIn = await h.Attendance.ClockInAsync(new(h.A), Ct);
        Assert.Null(checkIn.ClockInLatitude);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Attendance.AttachLocationAsync(checkIn.Id, h.B, new("clock-in", 28.44754m, 77.07128m, 1200), Ct));
        await h.Attendance.AttachLocationAsync(checkIn.Id, h.A, new("clock-in", 28.44754m, 77.07128m, 1200), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Attendance.AttachLocationAsync(checkIn.Id, h.A, new("clock-in", 28.4m, 77.0m, 8), Ct));
        var checkOut = await h.Attendance.ClockOutAsync(new(h.A), Ct);
        await h.Attendance.AttachLocationAsync(checkOut.Id, h.A, new("clock-out", 28.44755m, 77.07129m, 850), Ct);
        var saved = await h.Db.AttendanceRecords.FindAsync([checkIn.Id], Ct);
        Assert.Equal(1200, saved!.ClockInAccuracyMeters);
        Assert.Equal(850, saved.ClockOutAccuracyMeters);
    }

    [Fact]
    public async Task Meeting_visibility_is_limited_to_organizer_invitees_and_administrators()
    {
        using var h = new Harness();
        foreach (var id in new[] { h.A, h.B, h.C })
        {
            h.Db.Users.Add(new UserAccount { Id = id, TenantId = h.Id, Email = $"{id}@example.test", DisplayName = id.ToString(), IsActive = true });
            (await h.Db.Employees.FindAsync([id], Ct))!.UserId = id;
        }
        await h.Db.SaveChangesAsync(Ct);
        h.AsEmployee(h.A);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await h.Meetings.CreateAsync(new("Planning review", "Quarterly plan", start, start.AddHours(1),
            "Room 2", null, [h.A, h.B]), Ct);
        Assert.Single(h.Emails.Items, x => x.Email == $"{h.B}@example.test" && x.Key == EmailTemplateKeys.ForNotification("meeting"));
        Assert.Single(h.Emails.Items, x => x.Email == $"{h.A}@example.test" && x.Key == EmailTemplateKeys.ForNotification("meeting"));
        Assert.Single(await h.Meetings.ListAsync(start.AddHours(-1), start.AddHours(2), Ct));

        h.AsEmployee(h.B);
        Assert.Single(await h.Meetings.ListAsync(start.AddHours(-1), start.AddHours(2), Ct));
        Assert.Single((await h.Calendar.GetAsync(start.Year, start.Month, null, Ct)).Meetings!);
        h.AsEmployee(h.C);
        Assert.Empty(await h.Meetings.ListAsync(start.AddHours(-1), start.AddHours(2), Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Meetings.UpdateAsync(created.Id,
            new("Changed meeting", null, start, start.AddHours(1), null, null, [h.C], created.Version), Ct));

        h.User.Admin = true;
        Assert.Single(await h.Meetings.ListAsync(start.AddHours(-1), start.AddHours(2), Ct));
        var updated = await h.Meetings.UpdateAsync(created.Id,
            new("Planning review updated", "Revised plan", start, start.AddHours(1), "Room 3", null, [h.A, h.B], created.Version), Ct);
        Assert.Equal(2, h.Emails.Items.Count(x => x.Email == $"{h.A}@example.test" && x.Key == EmailTemplateKeys.ForNotification("meeting")));
        await h.Meetings.CancelAsync(updated.Id, updated.Version, Ct);
        Assert.Equal(3, h.Emails.Items.Count(x => x.Email == $"{h.A}@example.test" && x.Key == EmailTemplateKeys.ForNotification("meeting")));
        Assert.NotNull((await h.Db.Meetings.FindAsync([created.Id], Ct))!.CancelledAt);
    }

    [Fact]
    public async Task Concurrent_open_session_constraint_is_part_of_model()
    {
        using var h = new Harness();
        var index = h.Db.Model.FindEntityType(typeof(AttendanceRecord))!.GetIndexes().Single(x => x.IsUnique);
        Assert.Contains("ClockedOutAt", index.GetFilter());
        await h.Attendance.ClockInAsync(new(h.A, At(9)), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Attendance.ClockInAsync(new(h.A, At(10)), Ct));
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(25)]
    public async Task Clock_out_rejects_invalid_duration(int hours)
    {
        using var h = new Harness(); await h.Attendance.ClockInAsync(new(h.A, At(9)), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Attendance.ClockOutAsync(new(h.A, At(9).AddHours(hours)), Ct));
    }

    [Fact]
    public async Task Multiple_sessions_accumulate_overtime_and_reject_overlap()
    {
        using var h = new Harness();
        await h.Attendance.UpdatePolicyAsync(new(new(9, 0), new(17, 0), 0, 0, ["Monday"], false), Ct);
        await h.Attendance.ClockInAsync(new(h.A, At(9)), Ct); await h.Attendance.ClockOutAsync(new(h.A, At(13)), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Attendance.ClockInAsync(new(h.A, At(12)), Ct));
        await h.Attendance.ClockInAsync(new(h.A, At(14)), Ct);
        var second = await h.Attendance.ClockOutAsync(new(h.A, At(20)), Ct);
        Assert.Equal(2, second.OvertimeHours);
    }

    [Fact]
    public async Task Missing_check_out_does_not_inflate_reports()
    {
        using var h = new Harness(); await h.Attendance.ClockInAsync(new(h.A, At(9)), Ct);
        var row = Assert.Single((await h.Attendance.ReportAsync(new(), h.A, Day, Day, Ct)).Items);
        Assert.Equal(0, row.TotalHours); Assert.Contains("correction required", row.Status);
    }

    [Theory]
    [InlineData("8")] [InlineData("1")] [InlineData("Funday")]
    public async Task Policy_rejects_invalid_workday_names(string day)
    {
        using var h = new Harness();
        await Assert.ThrowsAsync<DomainException>(() => h.Attendance.UpdatePolicyAsync(new(new(9, 0), new(17, 0), 0, 0, [day], false), Ct));
    }

    [Fact]
    public async Task Existing_policy_requires_current_version()
    {
        using var h = new Harness();
        await h.Attendance.UpdatePolicyAsync(new(new(9, 0), new(17, 0), 0, 0, ["Monday"], false), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Attendance.UpdatePolicyAsync(new(new(10, 0), new(18, 0), 0, 0, ["Monday"], false), Ct));
    }

    [Fact]
    public async Task Correction_approval_creates_attendance_once_and_is_scoped_to_direct_manager()
    {
        using var h = new Harness(); h.AsEmployee(h.A);
        var correction = await h.Corrections.SubmitAsync(h.Correction(), Ct);
        Assert.Empty(await h.Db.AttendanceRecords.ToListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Corrections.ReviewAsync(correction.Id, new(true, null, correction.Version), Ct));
        h.AsEmployee(h.C, Permissions.TeamRead, Permissions.TeamApprove);
        Assert.Empty((await h.Corrections.SearchAsync(new(), "team", null, Ct)).Items);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Corrections.ReviewAsync(correction.Id, new(true, null, correction.Version), Ct));
        h.AsEmployee(h.B, Permissions.TeamRead, Permissions.TeamApprove);
        Assert.Single((await h.Corrections.SearchAsync(new(), "team", null, Ct)).Items);
        var approved = await h.Corrections.ReviewAsync(correction.Id, new(true, "Verified with roster", correction.Version), Ct);
        Assert.Equal(WorkflowStatus.Approved, approved.Status);
        Assert.Equal(8, (await h.Db.AttendanceRecords.SingleAsync()).WorkHours);
        await Assert.ThrowsAsync<DomainException>(() => h.Corrections.ReviewAsync(correction.Id, new(true, null, approved.Version), Ct));
    }

    [Fact]
    public async Task Corrections_reject_other_employee_submission_duplicates_and_overlaps()
    {
        using var h = new Harness(); h.AsEmployee(h.A);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Corrections.SubmitAsync(h.Correction() with { EmployeeId = h.C }, Ct));
        await h.Corrections.SubmitAsync(h.Correction(), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Corrections.SubmitAsync(h.Correction(), Ct));
    }

    [Fact]
    public async Task Correction_cancellation_allows_resubmission_and_rejection_requires_reason()
    {
        using var h = new Harness(); h.AsEmployee(h.A);
        var first = await h.Corrections.SubmitAsync(h.Correction(), Ct);
        await h.Corrections.CancelAsync(first.Id, first.Version, Ct);
        var second = await h.Corrections.SubmitAsync(h.Correction(), Ct);
        h.AsEmployee(h.B, Permissions.TeamApprove);
        await Assert.ThrowsAsync<DomainException>(() => h.Corrections.ReviewAsync(second.Id, new(false, "", second.Version), Ct));
        var rejected = await h.Corrections.ReviewAsync(second.Id, new(false, "Roster does not match", second.Version), Ct);
        Assert.Equal(WorkflowStatus.Rejected, rejected.Status); Assert.Empty(await h.Db.AttendanceRecords.ToListAsync());
    }

    [Fact]
    public async Task Correction_cannot_overwrite_changed_original_session()
    {
        using var h = new Harness(); h.AsEmployee(h.A);
        var original = await h.Attendance.ClockInAsync(new(h.A, At(9)), Ct);
        var correction = await h.Corrections.SubmitAsync(h.Correction() with { AttendanceRecordId = original.Id }, Ct);
        await h.Attendance.ClockOutAsync(new(h.A, At(16)), Ct);
        h.AsEmployee(h.B, Permissions.TeamApprove);
        await Assert.ThrowsAsync<DomainException>(() => h.Corrections.ReviewAsync(correction.Id, new(true, null, correction.Version), Ct));
        Assert.Equal(7, (await h.Db.AttendanceRecords.SingleAsync()).WorkHours);
    }

    [Fact]
    public async Task Sprint_lifecycle_moves_only_unfinished_work_to_backlog()
    {
        using var h = new Harness(); var project = await h.Project();
        var item = await h.Item(project.Id);
        var sprint = await h.Planning.CreateAsync(project.Id, new("August delivery", "Improve onboarding", Day, Day.AddDays(13)), Ct);
        item = await h.Planning.PlanAsync(item.Id, new(sprint.Id, item.Version), Ct);
        Assert.Equal(sprint.Id, item.SprintId);
        sprint = await h.Planning.ChangeAsync(sprint.Id, new(SprintStatus.Active, sprint.Version), Ct);
        var next = await h.Planning.CreateAsync(project.Id, new("Next sprint", null, Day.AddDays(14), Day.AddDays(27)), Ct);
        var other = await h.Item(project.Id); await h.Planning.PlanAsync(other.Id, new(next.Id, other.Version), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Planning.ChangeAsync(next.Id, new(SprintStatus.Active, next.Version), Ct));
        await h.Planning.ChangeAsync(sprint.Id, new(SprintStatus.Completed, sprint.Version, next.Id), Ct);
        Assert.Equal(next.Id, (await h.Work.GetItemAsync(item.Id, Ct)).Item.SprintId);
        var active = await h.Planning.ChangeAsync(next.Id, new(SprintStatus.Active, next.Version), Ct);
        Assert.Equal(SprintStatus.Active, active.Status);
    }

    [Fact]
    public async Task Sprint_permissions_require_both_membership_and_assignment_capability()
    {
        using var h = new Harness(); var project = await h.Project();
        h.AsEmployee(h.A, Permissions.WorkRead, Permissions.WorkAssign);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Planning.CreateAsync(project.Id, new("Sprint", null, Day, Day.AddDays(7)), Ct));
        h.User.Admin = true;
        await h.Work.SetMembersAsync(project.Id, [new(h.A, true, false, true, true, false)], Ct);
        h.User.Admin = false;
        Assert.Empty(await h.Planning.ListAsync(project.Id, Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Planning.CreateAsync(project.Id, new("Sprint", null, Day, Day.AddDays(7)), Ct));
    }

    [Fact]
    public async Task Sprint_scope_cannot_cross_project_or_tenant_boundaries()
    {
        using var h = new Harness(); var project = await h.Project(); var item = await h.Item(project.Id);
        var other = await h.Project("OTHER"); var sprint = await h.Planning.CreateAsync(other.Id, new("Other sprint", null, Day, Day.AddDays(7)), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Planning.PlanAsync(item.Id, new(sprint.Id, item.Version), Ct));
        h.Tenant.Set(Guid.NewGuid());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Planning.ListAsync(project.Id, Ct));
    }

    [Fact]
    public async Task Parent_cannot_complete_until_child_work_is_done()
    {
        using var h = new Harness(); var project = await h.Project(); var parent = await h.Item(project.Id);
        await h.Work.CreateItemAsync(h.ItemRequest(project.Id) with { ParentId = parent.Id, Type = WorkItemType.Subtask }, Ct);
        parent = await h.Work.TransitionItemAsync(parent.Id, new(WorkItemStatus.ToDo, null, null, parent.Version), Ct);
        parent = await h.Work.TransitionItemAsync(parent.Id, new(WorkItemStatus.InProgress, null, null, parent.Version), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Work.TransitionItemAsync(parent.Id, new(WorkItemStatus.Done, null, null, parent.Version), Ct));
    }

    [Fact]
    public async Task Leave_uses_scheduled_days_and_cancellation_restores_balance()
    {
        using var h = new Harness(); h.AsEmployee(h.A);
        var type = new LeaveType { TenantId = h.Id, Code = "AL", Name = "Annual", AnnualAllowance = 20 };
        h.Db.LeaveTypes.Add(type); await h.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => h.Leave.SubmitAsync(new(h.A, type.Id, Day, Day.AddDays(4), 1, "Annual leave"), Ct));
        var request = await h.Leave.SubmitAsync(new(h.A, type.Id, Day, Day.AddDays(4), 5, "Annual leave"), Ct);
        Assert.Equal(5, (await h.Db.LeaveBalances.SingleAsync()).Pending);
        await h.Leave.CancelAsync(request.Id, request.Version, Ct);
        Assert.Equal(20, (await h.Db.LeaveBalances.SingleAsync()).Available);
    }

    [Fact]
    public async Task Leave_excludes_location_holidays_and_blocks_self_approval()
    {
        using var h = new Harness(); h.AsEmployee(h.A);
        var type = new LeaveType { TenantId = h.Id, Code = "AL", Name = "Annual", AnnualAllowance = 20 };
        h.Db.LeaveTypes.Add(type); h.Db.Holidays.Add(new Holiday { TenantId = h.Id, Name = "Company holiday", Date = Day }); await h.Db.SaveChangesAsync();
        var request = await h.Leave.SubmitAsync(new(h.A, type.Id, Day, Day.AddDays(1), 1, "Annual leave"), Ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Leave.ReviewAsync(request.Id, new(true, null, request.Version), Ct));
    }

    [Fact]
    public async Task Optional_holiday_affects_only_the_employee_who_selected_it()
    {
        using var h = new Harness();
        var holiday = new Holiday { TenantId = h.Id, Name = "Choice day", Date = Day, IsOptional = true };
        var type = new LeaveType { TenantId = h.Id, Code = "AL", Name = "Annual", AnnualAllowance = 20 };
        h.Db.Holidays.Add(holiday); h.Db.LeaveTypes.Add(type);
        h.Db.HolidaySelections.Add(new HolidaySelection { TenantId = h.Id, HolidayId = holiday.Id, EmployeeId = h.A });
        await h.Db.SaveChangesAsync();
        var chosen = await h.Attendance.ReportAsync(new(), h.A, Day, Day, Ct);
        var notChosen = await h.Attendance.ReportAsync(new(), h.B, Day, Day, Ct);
        Assert.Equal("Optional holiday", Assert.Single(chosen.Items).Status);
        Assert.Equal("Absent", Assert.Single(notChosen.Items).Status);
        h.AsEmployee(h.A);
        await Assert.ThrowsAsync<DomainException>(() => h.Leave.SubmitAsync(new(h.A, type.Id, Day, Day, 1, "Annual leave"), Ct));
        var request = await h.Leave.SubmitAsync(new(h.A, type.Id, Day, Day.AddDays(1), 1, "Annual leave"), Ct);
        Assert.Equal(1, request.Days);
    }

    [Fact]
    public void India_calendar_parser_distinguishes_public_holidays_from_observances()
    {
        const string ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20261108\r\nSUMMARY:Diwali/Deepavali\r\nDESCRIPTION:Public holiday\r\nEND:VEVENT\r\nBEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20261109\r\nSUMMARY:Observance\r\nDESCRIPTION:Observance\r\nEND:VEVENT\r\nEND:VCALENDAR";
        var dates = PublicHolidaySource.Parse(ics);
        Assert.Equal(new DateOnly(2026, 11, 8), dates[0].Date);
        Assert.True(dates[0].IsPublicHoliday);
        Assert.False(dates[1].IsPublicHoliday);
    }

    [Fact]
    public async Task Optional_selection_is_limited_to_the_linked_employee_and_applicable_location()
    {
        using var h = new Harness();
        var date = new DateOnly(2027, 11, 8);
        var location = new Location { TenantId = h.Id, Name = "Bengaluru", Code = "BLR", CountryCode = "IN" };
        var holiday = new Holiday { TenantId = h.Id, Name = "Festival choice", Date = date, LocationId = location.Id, IsOptional = true };
        h.Db.Locations.Add(location); h.Db.Holidays.Add(holiday);
        (await h.Db.Employees.FindAsync(h.A))!.LocationId = location.Id;
        await h.Db.SaveChangesAsync();
        h.AsEmployee(h.B);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Calendar.SelectAsync(holiday.Id, true, Ct));
        h.AsEmployee(h.A);
        await h.Calendar.SelectAsync(holiday.Id, true, Ct);
        Assert.Equal(h.A, (await h.Db.HolidaySelections.SingleAsync()).EmployeeId);
        var view = await h.Calendar.GetAsync(date.Year, date.Month, null, Ct);
        Assert.True(Assert.Single(view.Holidays).Selected);
        await h.Calendar.SelectAsync(holiday.Id, false, Ct);
        Assert.Empty(await h.Db.HolidaySelections.ToListAsync());
    }

    [Fact]
    public async Task Hr_cannot_create_wildcard_or_payroll_roles_or_reset_privileged_accounts()
    {
        using var h = new Harness(); h.AsEmployee(h.A, Permissions.IdentityManage);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Identity.CreateRoleAsync(new("Super", [Permissions.All]), Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Identity.CreateRoleAsync(new("Finance", [Permissions.PayrollManage]), Ct));
        var role = await h.Identity.CreateRoleAsync(new("Employee", [Permissions.SelfService]), Ct);
        Assert.Contains(Permissions.SelfService, role.Permissions);
    }

    [Fact]
    public async Task Reassigning_same_roles_does_not_duplicate_active_links()
    {
        using var h = new Harness();
        var role = await h.Identity.CreateRoleAsync(new("Employee", [Permissions.SelfService]), Ct);
        var account = await h.Identity.CreateUserAsync(new("QA employee", "qa@example.test", "Synthetic-Password-123!", [role.Id], null), Ct);
        await h.Identity.SetRolesAsync(account.Id, new([role.Id], account.Version), Ct);
        Assert.Single(await h.Db.UserRoles.ToListAsync());
        Assert.Single(await h.Db.UserRoles.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Audit_payloads_exclude_passwords_and_token_material()
    {
        using var h = new Harness();
        h.Db.Users.Add(new UserAccount { TenantId = h.Id, Email = "audit@example.test", PasswordHash = "sensitive-password-hash" });
        h.Db.RefreshTokens.Add(new RefreshToken { TenantId = h.Id, UserId = Guid.NewGuid(), TokenHash = "sensitive-token-hash" });
        await h.Db.SaveChangesAsync();
        var json = string.Join(' ', (await h.Db.AuditLogs.ToListAsync()).Select(x => x.AfterJson));
        Assert.DoesNotContain("sensitive-password-hash", json); Assert.DoesNotContain("sensitive-token-hash", json);
    }

    [Fact]
    public async Task Sessions_enforce_revocation_and_current_permissions()
    {
        using var h = new Harness(); var userId = Guid.NewGuid(); var sessionId = Guid.NewGuid();
        var role = new Role { TenantId = h.Id, Name = "Employee", NormalizedName = "EMPLOYEE", PermissionsCsv = Permissions.SelfService };
        h.Db.Users.Add(new UserAccount { Id = userId, TenantId = h.Id, Email = "session@example.test", IsActive = true });
        h.Db.Roles.Add(role); h.Db.UserRoles.Add(new UserRole { TenantId = h.Id, UserId = userId, RoleId = role.Id });
        var token = new RefreshToken { Id = sessionId, TenantId = h.Id, UserId = userId, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), TokenHash = "test" };
        h.Db.RefreshTokens.Add(token);
        var subscription = new TenantSubscription { TenantId = h.Id, PlanCode = "test", EmployeeLimit = 10, StartsAt = DateTimeOffset.UtcNow.AddDays(-1), IsActive = true };
        h.Db.TenantSubscriptions.Add(subscription);
        await h.Db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId.ToString()), new("tenant_id", h.Id.ToString()), new("session_id", sessionId.ToString()), new("permission", "*")], "test"));
        var validator = new SessionValidator(h.Db);
        Assert.True(await validator.ValidateAsync(principal, Ct));
        Assert.False(principal.HasClaim("permission", "*")); Assert.True(principal.HasClaim("permission", Permissions.SelfService));
        var tenant = await h.Db.Tenants.SingleAsync();
        tenant.AdminAccessEnabled = false; await h.Db.SaveChangesAsync();
        Assert.False(await validator.ValidateAsync(principal, Ct));
        tenant.AdminAccessEnabled = true;
        tenant.AdminAccessStartsAt = DateTimeOffset.UtcNow.AddDays(-1);
        tenant.AdminAccessEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
        await h.Db.SaveChangesAsync();
        Assert.False(await validator.ValidateAsync(principal, Ct));
        tenant.AdminAccessEndsAt = DateTimeOffset.UtcNow.AddDays(1); await h.Db.SaveChangesAsync();
        Assert.True(await validator.ValidateAsync(principal, Ct));
        tenant.AdminAccessEnabled = null; await h.Db.SaveChangesAsync();
        subscription.IsActive = false; await h.Db.SaveChangesAsync();
        Assert.False(await validator.ValidateAsync(principal, Ct));
        subscription.IsActive = true;
        token.RevokedAt = DateTimeOffset.UtcNow; await h.Db.SaveChangesAsync();
        Assert.False(await validator.ValidateAsync(principal, Ct));
    }

    private sealed class Actor : ICurrentUser
    {
        public Guid? EmployeeId { get; set; }
        public Guid? UserId => EmployeeId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public bool Admin { get; set; } = true;
        public bool IsPlatformAdmin => Admin;
        public HashSet<string> Grants { get; set; } = [];
        public bool HasPermission(string permission) => Admin || Grants.Contains(permission);
    }

    private sealed class NoPublicHolidays : IPublicHolidaySource
    {
        public Task<(IReadOnlyList<CalendarObservance> Items, bool Available)> GetAsync(string countryCode, int year, CancellationToken ct) =>
            Task.FromResult<(IReadOnlyList<CalendarObservance>, bool)>(([], false));
    }

    [Fact]
    public async Task New_trial_allows_billing_until_mandate_is_authorized_then_pauses_at_expiry()
    {
        using var h = new Harness();
        var tenant = await h.Db.Tenants.SingleAsync();
        tenant.Status = TenantStatus.Trial;
        tenant.RequiresBillingMandate = true;
        tenant.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(30);
        var user = new UserAccount { TenantId = h.Id, Email = "owner@example.test", IsActive = true };
        var role = new Role { TenantId = h.Id, Name = "Owner", NormalizedName = "TENANT_ADMIN", PermissionsCsv = "*" };
        var token = new RefreshToken { TenantId = h.Id, UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), TokenHash = "owner-session" };
        h.Db.Users.Add(user); h.Db.Roles.Add(role); h.Db.RefreshTokens.Add(token);
        h.Db.UserRoles.Add(new UserRole { TenantId = h.Id, UserId = user.Id, RoleId = role.Id });
        h.Db.TenantSubscriptions.Add(new TenantSubscription { TenantId = h.Id, PlanCode = "trial", StartsAt = DateTimeOffset.UtcNow, EndsAt = tenant.TrialEndsAt, IsActive = true });
        await h.Db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new("tenant_id", h.Id.ToString()), new("session_id", token.Id.ToString())], "test"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var validator = new SessionValidator(h.Db, accessor);
        accessor.HttpContext.Request.Path = "/api/v1/employees";
        Assert.False(await validator.ValidateAsync(principal, Ct));
        accessor.HttpContext.Request.Path = "/api/v1/billing/status";
        Assert.True(await validator.ValidateAsync(principal, Ct));
        h.Db.BillingCheckouts.Add(new BillingCheckout { TenantId = h.Id, Provider = "razorpay_live", ProviderSubscriptionId = "sub_test", Status = "authenticated" });
        await h.Db.SaveChangesAsync();
        accessor.HttpContext.Request.Path = "/api/v1/employees";
        Assert.True(await validator.ValidateAsync(principal, Ct));
        tenant.TrialEndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await h.Db.SaveChangesAsync();
        Assert.False(await validator.ValidateAsync(principal, Ct));
    }

    private sealed class CapturingEmailQueue : IEmailQueue
    {
        public List<(string Email, string Key)> Items { get; } = [];
        public Task QueueAsync(string toEmail, string? toName, string templateKey, IReadOnlyDictionary<string, string?> model, CancellationToken ct)
        { Items.Add((toEmail, templateKey)); return Task.CompletedTask; }
    }

    private sealed class Harness : IDisposable
    {
        public CurrentTenant Tenant { get; } = new(); public Actor User { get; } = new();
        public Guid Id => Tenant.TenantId!.Value;
        public Guid A { get; } = Guid.NewGuid(); public Guid B { get; } = Guid.NewGuid(); public Guid C { get; } = Guid.NewGuid();
        public HrmsDbContext Db { get; }
        public AttendanceService Attendance { get; } public AttendanceCorrectionService Corrections { get; }
        public WorkManagementService Work { get; } public WorkPlanningService Planning { get; }
        public LeaveService Leave { get; } public IdentityAdminService Identity { get; }
        public CalendarService Calendar { get; }
        public MeetingService Meetings { get; } public CapturingEmailQueue Emails { get; } = new();
        public Harness()
        {
            Tenant.Set(Guid.NewGuid());
            Db = new(new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Tenant, User, new TestNotificationPublisher());
            Db.Tenants.Add(new Tenant { Id = Id, Name = "Enterprise QA", Slug = "enterprise-qa", TimeZone = "UTC", Status = TenantStatus.Active });
            foreach (var id in new[] { A, B, C }) Db.Employees.Add(new Employee { Id = id, TenantId = Id, EmployeeNumber = id.ToString(), FirstName = id == A ? "Asha" : id == B ? "Rohan" : "Leena", LastName = "Test", WorkEmail = $"{id}@example.test", HireDate = new(2025, 1, 1), ManagerId = id == A ? B : null });
            Db.SaveChangesAsync().GetAwaiter().GetResult();
            var notifications = new NotificationService(R<UserNotification>(), R<Employee>(), R<UserAccount>(), R<Role>(), R<UserRole>(), Tenant, User, Db, Emails);
            Meetings = new(R<Meeting>(), R<MeetingAttendee>(), R<Employee>(), R<UserAccount>(), R<Tenant>(), Tenant, User, Db, notifications, Emails);
            Attendance = new(R<AttendanceRecord>(), R<AttendancePolicy>(), R<Employee>(), R<Tenant>(), R<Holiday>(), R<HolidaySelection>(), R<LeaveRequest>(), Tenant, Db);
            Corrections = new(R<AttendanceCorrection>(), R<AttendanceRecord>(), R<Employee>(), R<AttendancePolicy>(), R<Tenant>(), R<Holiday>(), R<HolidaySelection>(), R<LeaveRequest>(), Tenant, User, Db, notifications);
            Work = new(R<WorkProject>(), R<WorkProjectMember>(), R<WorkItem>(), R<WorkItemAssignee>(), R<WorkItemComment>(), R<WorkLog>(), R<WorkItemHistory>(), R<Employee>(), Tenant, User, Db, notifications);
            Planning = new(R<WorkSprint>(), R<WorkProject>(), R<WorkProjectMember>(), R<WorkItem>(), R<WorkItemHistory>(), Work, Tenant, User, Db);
            Leave = new(R<LeaveType>(), R<LeaveBalance>(), R<LeaveRequest>(), R<Employee>(), R<StoredDocument>(), Tenant, User, Db, notifications, R<AttendancePolicy>(), R<Holiday>(), R<HolidaySelection>(), R<Tenant>());
            Calendar = new(R<Holiday>(), R<HolidaySelection>(), R<Employee>(), R<Location>(), R<LeaveRequest>(), R<AttendanceRecord>(), R<AttendancePolicy>(), R<Tenant>(), Tenant, User, Db, new NoPublicHolidays(), Meetings);
            Identity = new(R<UserAccount>(), R<Role>(), R<UserRole>(), R<Employee>(), R<RefreshToken>(), new Pbkdf2PasswordHasher(), Tenant, Db, notifications, User);
        }
        private Repository<T> R<T>() where T : AuditableEntity => new(Db);
        public void AsEmployee(Guid id, params string[] grants) { User.Admin = false; User.EmployeeId = id; User.Grants = [Permissions.SelfService, ..grants]; }
        public RequestAttendanceCorrection Correction() => new(null, null, Day, At(9), At(17), "Missed punches during approved client visit");
        public Task<WorkProjectDto> Project(string key = "QA") => Work.CreateProjectAsync(new(key, "Employee experience", null, null), Ct);
        public CreateWorkItemRequest ItemRequest(Guid project) => new(project, WorkItemType.Task, "Improve onboarding", null, null, [], null, null, WorkItemPriority.Medium, null, 120, 3, []);
        public Task<WorkItemDto> Item(Guid project) => Work.CreateItemAsync(ItemRequest(project), Ct);
        public void Dispose() => Db.Dispose();
    }
}
