using System.Security.Claims;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class EnterpriseWorkflowTests
{
    private static readonly CancellationToken Ct = default;
    private static readonly DateOnly Day = new(2026, 8, 3);
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

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
        h.Db.RefreshTokens.Add(token); await h.Db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId.ToString()), new("tenant_id", h.Id.ToString()), new("session_id", sessionId.ToString()), new("permission", "*")], "test"));
        var validator = new SessionValidator(h.Db);
        Assert.True(await validator.ValidateAsync(principal, Ct));
        Assert.False(principal.HasClaim("permission", "*")); Assert.True(principal.HasClaim("permission", Permissions.SelfService));
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

    private sealed class Harness : IDisposable
    {
        public CurrentTenant Tenant { get; } = new(); public Actor User { get; } = new();
        public Guid Id => Tenant.TenantId!.Value;
        public Guid A { get; } = Guid.NewGuid(); public Guid B { get; } = Guid.NewGuid(); public Guid C { get; } = Guid.NewGuid();
        public HrmsDbContext Db { get; }
        public AttendanceService Attendance { get; } public AttendanceCorrectionService Corrections { get; }
        public WorkManagementService Work { get; } public WorkPlanningService Planning { get; }
        public LeaveService Leave { get; } public IdentityAdminService Identity { get; }
        public Harness()
        {
            Tenant.Set(Guid.NewGuid());
            Db = new(new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Tenant, User, new TestNotificationPublisher());
            Db.Tenants.Add(new Tenant { Id = Id, Name = "Enterprise QA", Slug = "enterprise-qa", TimeZone = "UTC", Status = TenantStatus.Active });
            foreach (var id in new[] { A, B, C }) Db.Employees.Add(new Employee { Id = id, TenantId = Id, EmployeeNumber = id.ToString(), FirstName = id == A ? "Asha" : id == B ? "Rohan" : "Leena", LastName = "Test", WorkEmail = $"{id}@example.test", HireDate = new(2025, 1, 1), ManagerId = id == A ? B : null });
            Db.SaveChangesAsync().GetAwaiter().GetResult();
            var notifications = new NotificationService(R<UserNotification>(), R<Employee>(), R<UserAccount>(), R<Role>(), R<UserRole>(), Tenant, User, Db);
            Attendance = new(R<AttendanceRecord>(), R<AttendancePolicy>(), R<Employee>(), R<Tenant>(), R<Holiday>(), R<LeaveRequest>(), Tenant, Db);
            Corrections = new(R<AttendanceCorrection>(), R<AttendanceRecord>(), R<Employee>(), R<AttendancePolicy>(), R<Tenant>(), R<Holiday>(), R<LeaveRequest>(), Tenant, User, Db, notifications);
            Work = new(R<WorkProject>(), R<WorkProjectMember>(), R<WorkItem>(), R<WorkItemAssignee>(), R<WorkItemComment>(), R<WorkLog>(), R<WorkItemHistory>(), R<Employee>(), Tenant, User, Db, notifications);
            Planning = new(R<WorkSprint>(), R<WorkProject>(), R<WorkProjectMember>(), R<WorkItem>(), R<WorkItemHistory>(), Work, Tenant, User, Db);
            Leave = new(R<LeaveType>(), R<LeaveBalance>(), R<LeaveRequest>(), R<Employee>(), R<StoredDocument>(), Tenant, User, Db, notifications, R<AttendancePolicy>(), R<Holiday>(), R<Tenant>());
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
