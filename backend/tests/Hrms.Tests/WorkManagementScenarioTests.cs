using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

// Each scenario uses a unique mock database, real repositories, tenant filters,
// soft deletion, versioning and notification service. No company database is touched.
public sealed class WorkManagementScenarioTests
{
    private static readonly CancellationToken Ct = default;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Project_keys_are_normalized_unique_and_lead_is_a_member()
    {
        using var h = new Harness();
        var p = await h.Service.CreateProjectAsync(new(" qa ", " QA project ", null, h.A), Ct);
        Assert.Equal("QA", p.Key);
        Assert.Equal("QA project", p.Name);
        Assert.Single(await h.Service.ListMembersAsync(p.Id, Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.CreateProjectAsync(new("qa", "Duplicate", null, null), Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1BAD")]
    [InlineData("A")]
    [InlineData("TOOLONGPROJECT")]
    [InlineData("QA-X")]
    public async Task Invalid_project_keys_are_rejected(string key)
    {
        using var h = new Harness();
        await Assert.ThrowsAsync<DomainException>(() => h.Service.CreateProjectAsync(new(key, "Test", null, null), Ct));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("assign")]
    [InlineData("transition")]
    [InlineData("log")]
    public async Task Project_flags_and_role_permissions_are_both_required_and_revocation_is_immediate(string operation)
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        await h.Access(p.Id, false);
        h.AsEmployee();
        await Assert.ThrowsAsync<DomainException>(() => h.Act(operation, p.Id, item));
        h.AsAdmin();
        await h.Access(p.Id, true);
        h.AsEmployee();
        var permission = operation switch { "create" => Permissions.WorkCreate, "assign" => Permissions.WorkAssign, "transition" => Permissions.WorkTransition, _ => Permissions.WorkLog };
        h.User.Grants.Remove(permission);
        await Assert.ThrowsAsync<DomainException>(() => h.Act(operation, p.Id, item));
        h.User.Grants.Add(permission);
        await h.Act(operation, p.Id, item);
        h.AsAdmin();
        await h.Access(p.Id, false);
        h.AsEmployee();
        item = (await h.Service.GetItemAsync(item.Id, Ct)).Item;
        await Assert.ThrowsAsync<DomainException>(() => h.Act(operation, p.Id, item));
    }

    [Fact]
    public async Task Membership_can_be_saved_repeatedly_removed_and_readded_without_duplicate_active_rows()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        await h.Access(p.Id, true);
        await h.Access(p.Id, true);
        Assert.Equal(2, await h.Db.WorkProjectMembers.CountAsync());
        await h.Service.SetMembersAsync(p.Id, [], Ct);
        h.AsEmployee();
        Assert.Empty(await h.Service.ListProjectsAsync(Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.GetItemAsync(item.Id, Ct));
        h.AsAdmin();
        await h.Access(p.Id, true);
        Assert.Equal(2, await h.Db.WorkProjectMembers.CountAsync());
        await Assert.ThrowsAsync<DomainException>(() => h.Service.SetMembersAsync(p.Id, [h.Member(h.A), h.Member(h.A)], Ct));
    }

    [Fact]
    public async Task Multi_assignment_deduplicates_and_explicit_empty_list_clears_legacy_primary()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        item = await h.Service.AssignItemAsync(item.Id, new(h.A, [h.A, h.B, h.A], item.Version), Ct);
        Assert.Equal(2, item.AssigneeEmployeeIds.Count);
        item = await h.Service.AssignItemAsync(item.Id, new(h.A, [], item.Version), Ct);
        Assert.Empty(item.AssigneeEmployeeIds);
        Assert.Null(item.AssigneeEmployeeId);
        item = await h.Service.AssignItemAsync(item.Id, new(h.B, [h.B], item.Version), Ct);
        Assert.Single(item.AssigneeEmployeeIds);
    }

    [Fact]
    public async Task Assigning_nonmember_and_cross_project_parent_is_rejected()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        await h.Service.SetMembersAsync(p.Id, [h.Member(h.A)], Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.AssignItemAsync(item.Id, new(h.B, [h.B], item.Version), Ct));
        var other = await h.Service.CreateProjectAsync(new("OTHER", "Other", null, null), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.CreateItemAsync(h.Request(other.Id) with { ParentId = item.Id }, Ct));
    }

    [Fact]
    public async Task Details_reject_negative_remaining_estimate()
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.UpdateItemAsync(item.Id,
            new(item.Type, item.Summary, null, item.Priority, null, 120, -1, null, [], null, null, h.B, item.Version), Ct));
    }

    [Fact]
    public async Task View_all_logs_does_not_allow_editing_or_deleting_another_employees_log()
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        h.AsEmployee(h.B);
        var log = await h.Service.AddWorklogAsync(item.Id, new(Today, 60, "Other employee", null), Ct);
        h.AsEmployee();
        Assert.Single((await h.Service.GetItemAsync(item.Id, Ct)).Worklogs);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.UpdateWorklogAsync(item.Id, log.Id, new(Today, 90, "Changed", null, log.Version), Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.DeleteWorklogAsync(item.Id, log.Id, Ct));
    }

    [Fact]
    public async Task Worklog_totals_cannot_exceed_24_hours_across_tasks_on_one_day()
    {
        using var h = new Harness();
        var p = await h.Project();
        var first = await h.Item(p.Id);
        var second = await h.Item(p.Id);
        h.AsEmployee();
        await h.Service.AddWorklogAsync(first.Id, new(Today, 1000, null, null), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.AddWorklogAsync(second.Id, new(Today, 500, null, null), Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public async Task Invalid_worklog_durations_are_rejected(int minutes)
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.AddWorklogAsync(item.Id, new(Today, minutes, null, null), Ct));
    }

    [Fact]
    public async Task Workflow_comments_reports_search_and_stale_versions_work_together()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.TransitionItemAsync(item.Id, new(WorkItemStatus.Done, null, null, item.Version), Ct));
        foreach (var status in new[] { WorkItemStatus.ToDo, WorkItemStatus.InProgress, WorkItemStatus.InReview, WorkItemStatus.Done })
            item = await h.Service.TransitionItemAsync(item.Id, new(status, null, "Status audit", item.Version), Ct);
        Assert.NotNull(item.ResolvedAt);
        item = await h.Service.TransitionItemAsync(item.Id, new(WorkItemStatus.InProgress, null, null, item.Version), Ct);
        Assert.Null(item.ResolvedAt);
        Assert.Null(item.Resolution);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.AssignItemAsync(item.Id, new(h.A, [h.A], 0), Ct));
        h.AsEmployee();
        var comment = await h.Service.AddCommentAsync(item.Id, new("Test comment"), Ct);
        comment = await h.Service.UpdateCommentAsync(item.Id, comment.Id, new("Edited", comment.Version), Ct);
        Assert.Equal("Edited", comment.Body);
        var log = await h.Service.AddWorklogAsync(item.Id, new(Today, 60, "Test work", null), Ct);
        log = await h.Service.UpdateWorklogAsync(item.Id, log.Id, new(Today, 90, "Updated work", null, log.Version), Ct);
        Assert.Equal(90, (await h.Service.GetTimeReportAsync(Today, Today, p.Id, h.A, Ct)).TotalMinutes);
        Assert.Single((await h.Service.SearchItemsAsync(new(1, 10, "Mock"), p.Id, WorkItemStatus.InProgress, null, null, Ct)).Items);
        await h.Service.DeleteWorklogAsync(item.Id, log.Id, Ct);
        await h.Service.DeleteCommentAsync(item.Id, comment.Id, Ct);
        Assert.Equal(0, (await h.Service.GetTimeReportAsync(Today, Today, p.Id, null, Ct)).TotalMinutes);
        Assert.NotEmpty((await h.Service.GetItemAsync(item.Id, Ct)).History);
    }

    [Fact]
    public async Task Private_logs_are_hidden_in_details_reports_and_overview()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        h.AsEmployee(h.B);
        await h.Service.AddWorklogAsync(item.Id, new(Today, 60, "Private work", null), Ct);
        h.AsEmployee();
        h.User.Grants.Remove(Permissions.WorkViewAllLogs);
        Assert.Empty((await h.Service.GetItemAsync(item.Id, Ct)).Worklogs);
        Assert.Equal(0, (await h.Service.GetTimeReportAsync(Today, Today, p.Id, null, Ct)).TotalMinutes);
        Assert.Equal(0, (await h.Service.GetOverviewAsync(Ct)).LoggedMinutesThisMonth);
    }

    [Fact]
    public async Task Tenant_switch_hides_projects_items_and_members()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        h.Tenant.Set(Guid.NewGuid());
        Assert.Empty(await h.Service.ListProjectsAsync(Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.GetItemAsync(item.Id, Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.ListMembersAsync(p.Id, Ct));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("assign")]
    [InlineData("transition")]
    [InlineData("log")]
    public async Task Inactive_projects_remain_readable_but_reject_changes(string operation)
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        p = (await h.Service.ListProjectsAsync(Ct)).Single();
        await h.Service.UpdateProjectAsync(p.Id, new(p.Name, null, null, false, p.Version), Ct);
        Assert.Equal(item.Id, (await h.Service.GetItemAsync(item.Id, Ct)).Item.Id);
        await Assert.ThrowsAsync<DomainException>(() => h.Act(operation, p.Id, item));
    }

    [Theory]
    [InlineData(EmploymentStatus.Suspended)]
    [InlineData(EmploymentStatus.Resigned)]
    [InlineData(EmploymentStatus.Terminated)]
    public async Task Inactive_employees_cannot_be_assigned(EmploymentStatus status)
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        (await h.Db.Employees.SingleAsync(x => x.Id == h.B)).Status = status;
        await h.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => h.Service.AssignItemAsync(item.Id, new(h.B, [h.B], item.Version), Ct));
    }

    [Fact]
    public async Task Invalid_enum_values_are_rejected_without_server_errors()
    {
        using var h = new Harness();
        var p = await h.Project();
        Assert.False(WorkWorkflow.CanTransition((WorkItemStatus)999, WorkItemStatus.ToDo));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.CreateItemAsync(h.Request(p.Id) with { Type = (WorkItemType)999 }, Ct));
    }

[Fact]
    public async Task Comments_cannot_be_modified_by_other_employees()
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        h.AsEmployee(h.B);
        var comment = await h.Service.AddCommentAsync(item.Id, new("Bob's note"), Ct);
        h.AsEmployee();
        await Assert.ThrowsAsync<DomainException>(() => h.Service.UpdateCommentAsync(item.Id, comment.Id, new("Changed", comment.Version), Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.DeleteCommentAsync(item.Id, comment.Id, Ct));
    }

    [Fact]
    public async Task Revoking_membership_blocks_every_task_mutation()
    {
        using var h = new Harness();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        await h.Service.SetMembersAsync(p.Id, [h.Member(h.B)], Ct);
        h.AsEmployee();
        foreach (var operation in new[] { "create", "assign", "transition", "log" })
            await Assert.ThrowsAsync<DomainException>(() => h.Act(operation, p.Id, item));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.AddCommentAsync(item.Id, new("No access"), Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.GetTimeReportAsync(Today, Today, p.Id, null, Ct));
    }

    [Fact]
    public async Task Worklog_edit_checks_daily_total_and_does_not_count_itself_twice()
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        var first = await h.Service.AddWorklogAsync(item.Id, new(Today, 700, null, null), Ct);
        await h.Service.AddWorklogAsync(item.Id, new(Today, 700, null, null), Ct);
        first = await h.Service.UpdateWorklogAsync(item.Id, first.Id, new(Today, 740, null, null, first.Version), Ct);
        await Assert.ThrowsAsync<DomainException>(() => h.Service.UpdateWorklogAsync(item.Id, first.Id, new(Today, 741, null, null, first.Version), Ct));
    }

    [Fact]
    public async Task Reports_reject_reversed_and_excessive_date_ranges()
    {
        using var h = new Harness();
        await Assert.ThrowsAsync<DomainException>(() => h.Service.GetTimeReportAsync(Today, Today.AddDays(-1), null, null, Ct));
        await Assert.ThrowsAsync<DomainException>(() => h.Service.GetTimeReportAsync(Today.AddDays(-367), Today, null, null, Ct));
    }

    [Fact]
    public async Task Assignment_notifies_new_assignees_and_reporter_with_mock_accounts()
    {
        using var h = new Harness();
        var recipient = Guid.NewGuid();
        var employee = await h.Db.Employees.SingleAsync(x => x.Id == h.B);
        employee.UserId = recipient;
        h.Db.Set<UserAccount>().Add(new UserAccount { Id = recipient, TenantId = h.Tenant.TenantId!.Value, Email = "bob@example.test", DisplayName = "Mock Bob" });
        await h.Db.SaveChangesAsync();
        var p = await h.Project();
        var item = await h.Item(p.Id);
        await h.Service.AssignItemAsync(item.Id, new(h.B, [h.B, h.B], item.Version), Ct);
        Assert.Single(await h.Db.UserNotifications.Where(x => x.UserId == recipient && x.Title == "Ticket assigned").ToListAsync());
        Assert.Contains(await h.Db.UserNotifications.Where(x => x.UserId == recipient).ToListAsync(), x => x.Title != "Ticket assigned" && x.Kind == "work");
    }

    [Fact]
    public async Task Document_read_requires_work_role_even_when_membership_exists()
    {
        using var h = new Harness();
        var item = await h.Item((await h.Project()).Id);
        var docs = new DocumentService(new Repository<StoredDocument>(h.Db), new Repository<Employee>(h.Db),
            new Repository<WorkItem>(h.Db), new Repository<WorkProjectMember>(h.Db), new Repository<LeaveRequest>(h.Db),
            new Repository<ExpenseClaim>(h.Db), new Repository<Candidate>(h.Db), h.Tenant, h.User, new NoStorage(), h.Db);
        h.AsEmployee();
        Assert.Empty(await docs.ListAsync(DocumentOwnerType.WorkItem, item.Id, null, Ct));
        h.User.Grants.Remove(Permissions.WorkRead);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => docs.ListAsync(DocumentOwnerType.WorkItem, item.Id, null, Ct));
    }

    private sealed class NoStorage : IDocumentStorage
    {
        public Task SaveAsync(string key, Stream content, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }


    private sealed class Actor : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; set; }
        public bool IsPlatformAdmin => false;
        public bool Admin { get; set; } = true;
        public HashSet<string> Grants { get; } = [Permissions.WorkRead, Permissions.WorkCreate, Permissions.WorkAssign, Permissions.WorkTransition, Permissions.WorkLog, Permissions.WorkComment, Permissions.WorkViewAllLogs];
        public bool HasPermission(string permission) => Admin || Grants.Contains(permission);
    }

    private sealed class Harness : IDisposable
    {
        public CurrentTenant Tenant { get; } = new();
        public Actor User { get; } = new();
        public HrmsDbContext Db { get; }
        public WorkManagementService Service { get; }
        public Guid A { get; } = Guid.NewGuid();
        public Guid B { get; } = Guid.NewGuid();
        public Harness()
        {
            Tenant.Set(Guid.NewGuid());
            User.EmployeeId = A;
            Db = new(new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Tenant, User, new TestNotificationPublisher());
            foreach (var id in new[] { A, B }) Db.Employees.Add(new Employee { Id = id, TenantId = Tenant.TenantId!.Value, FirstName = "Mock", LastName = id == A ? "Alice" : "Bob", EmployeeNumber = id.ToString(), WorkEmail = $"{id}@example.test", Status = EmploymentStatus.Active });
            Db.SaveChangesAsync().GetAwaiter().GetResult();
            var notifications = new NotificationService(new Repository<UserNotification>(Db), new Repository<Employee>(Db), new Repository<UserAccount>(Db), new Repository<Role>(Db), new Repository<UserRole>(Db), Tenant, User, Db);
            Service = new(new Repository<WorkProject>(Db), new Repository<WorkProjectMember>(Db), new Repository<WorkItem>(Db), new Repository<WorkItemAssignee>(Db), new Repository<WorkItemComment>(Db), new Repository<WorkLog>(Db), new Repository<WorkItemHistory>(Db), new Repository<Employee>(Db), Tenant, User, Db, notifications);
        }
        public void AsAdmin() => User.Admin = true;
        public void AsEmployee(Guid? id = null) { User.Admin = false; User.EmployeeId = id ?? A; }
        public SetWorkProjectMemberRequest Member(Guid id, bool allowed = true) => new(id, allowed, allowed, allowed, allowed, allowed);
        public Task<IReadOnlyList<WorkProjectMemberDto>> Access(Guid project, bool allowed) => Service.SetMembersAsync(project, [Member(A, allowed), Member(B)], Ct);
        public async Task<WorkProjectDto> Project() { var p = await Service.CreateProjectAsync(new("MOCK", "Mock project", null, null), Ct); await Access(p.Id, true); return p; }
        public CreateWorkItemRequest Request(Guid p) => new(p, WorkItemType.Task, "Mock task", null, null, [], B, null, WorkItemPriority.Medium, null, 120, null, []);
        public Task<WorkItemDto> Item(Guid p) => Service.CreateItemAsync(Request(p), Ct);
        public async Task Act(string operation, Guid project, WorkItemDto item)
        {
            switch (operation)
            {
                case "create": await Item(project); break;
                case "assign": await Service.AssignItemAsync(item.Id, new(B, [B], item.Version), Ct); break;
                case "transition": await Service.TransitionItemAsync(item.Id, new(WorkItemStatus.ToDo, null, null, item.Version), Ct); break;
                case "log": await Service.AddWorklogAsync(item.Id, new(Today, 30, null, null), Ct); break;
            }
        }
        public void Dispose() => Db.Dispose();
    }
}
