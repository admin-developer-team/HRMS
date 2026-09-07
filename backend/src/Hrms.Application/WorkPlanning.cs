using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record CreateSprintRequest(string Name, string? Goal, DateOnly StartsOn, DateOnly EndsOn);
public sealed record ChangeSprintRequest(SprintStatus Status, long Version, Guid? MoveUnfinishedToSprintId = null);
public sealed record PlanWorkItemRequest(Guid? SprintId, long Version);
public sealed record SprintDto(Guid Id, Guid ProjectId, string Name, string? Goal, DateOnly StartsOn, DateOnly EndsOn,
    SprintStatus Status, int TotalItems, int CompletedItems, decimal TotalPoints, decimal CompletedPoints, long Version);

public sealed class WorkPlanningService(IRepository<WorkSprint> sprints, IRepository<WorkProject> projects,
    IRepository<WorkProjectMember> members, IRepository<WorkItem> items, IRepository<WorkItemHistory> history,
    IWorkManagementService work, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork unitOfWork) : ServiceBase(tenant)
{
    public async Task<IReadOnlyList<SprintDto>> ListAsync(Guid projectId, CancellationToken ct)
    {
        await Access(projectId, false, ct);
        var rows = await sprints.ListAsync(x => x.ProjectId == projectId, q => q.OrderByDescending(x => x.StartsOn), cancellationToken: ct);
        return await Map(rows, ct);
    }

    public async Task<SprintDto> CreateAsync(Guid projectId, CreateSprintRequest r, CancellationToken ct)
    {
        var project = await Access(projectId, true, ct);
        Required(r.Name, "Sprint name");
        if (r.Name.Length > 100 || r.Goal?.Length > 2000) throw new DomainException("Sprint name or goal is too long.");
        if (r.EndsOn < r.StartsOn || r.EndsOn.DayNumber - r.StartsOn.DayNumber > 90) throw new DomainException("A sprint must last between 1 and 91 calendar days.");
        var row = new WorkSprint { TenantId = TenantId, ProjectId = projectId, Name = r.Name.Trim(), Goal = r.Goal?.Trim(), StartsOn = r.StartsOn, EndsOn = r.EndsOn };
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await sprints.AddAsync(row, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await Map([row], ct))[0];
    }

    public async Task<WorkItemDto> PlanAsync(Guid itemId, PlanWorkItemRequest r, CancellationToken ct)
    {
        var item = await items.GetByIdAsync(itemId, ct) ?? throw new KeyNotFoundException("Work item not found.");
        var project = await Access(item.ProjectId, true, ct);
        CheckVersion(item, r.Version);
        if (item.Status is WorkItemStatus.Done or WorkItemStatus.Cancelled) throw new DomainException("Reopen completed work before changing its sprint.");
        if (item.SprintId.HasValue && (await sprints.GetByIdAsync(item.SprintId.Value, ct))?.Status == SprintStatus.Completed)
            throw new DomainException("Completed sprint scope is preserved. Create follow-up work in a new sprint.");
        WorkSprint? target = null;
        if (r.SprintId.HasValue)
        {
            target = await sprints.GetByIdAsync(r.SprintId.Value, ct) ?? throw new KeyNotFoundException("Sprint not found.");
            if (target.ProjectId != item.ProjectId || target.Status == SprintStatus.Completed) throw new DomainException("Select an open sprint in this project.");
        }
        if (item.SprintId == r.SprintId) return (await work.GetItemAsync(item.Id, ct)).Item;
        await Record(item, "sprint", item.SprintId?.ToString(), target?.Id.ToString(), ct);
        item.SprintId = target?.Id;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        return (await work.GetItemAsync(item.Id, ct)).Item;
    }

    public async Task<SprintDto> ChangeAsync(Guid id, ChangeSprintRequest r, CancellationToken ct)
    {
        var sprint = await sprints.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Sprint not found.");
        var project = await Access(sprint.ProjectId, true, ct);
        CheckVersion(sprint, r.Version);
        if (sprint.Status == SprintStatus.Planned && r.Status == SprintStatus.Active)
        {
            if (await sprints.AnyAsync(x => x.ProjectId == sprint.ProjectId && x.Status == SprintStatus.Active, ct)) throw new DomainException("Complete the active sprint before starting another.");
            if (!await items.AnyAsync(x => x.SprintId == id && x.Status != WorkItemStatus.Done && x.Status != WorkItemStatus.Cancelled, ct)) throw new DomainException("Add unfinished work before starting a sprint.");
            sprint.Status = SprintStatus.Active; sprint.StartedAt = DateTimeOffset.UtcNow;
        }
        else if (sprint.Status == SprintStatus.Active && r.Status == SprintStatus.Completed)
        {
            WorkSprint? target = null;
            if (r.MoveUnfinishedToSprintId.HasValue)
            {
                target = await sprints.GetByIdAsync(r.MoveUnfinishedToSprintId.Value, ct) ?? throw new KeyNotFoundException("Destination sprint not found.");
                if (target.Id == id || target.ProjectId != sprint.ProjectId || target.Status == SprintStatus.Completed) throw new DomainException("Select another open sprint in this project.");
            }
            var unfinished = await items.ListAsync(x => x.SprintId == id && x.Status != WorkItemStatus.Done && x.Status != WorkItemStatus.Cancelled, cancellationToken: ct);
            foreach (var item in unfinished)
            {
                await Record(item, "sprint", sprint.Id.ToString(), target?.Id.ToString(), ct);
                item.SprintId = target?.Id;
            }
            sprint.Status = SprintStatus.Completed; sprint.CompletedAt = DateTimeOffset.UtcNow;
        }
        else throw new DomainException("Sprints progress from Planned to Active to Completed.");
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        return (await Map([sprint], ct))[0];
    }

    public async Task<IReadOnlyList<WorkItemDto>> ChildrenAsync(Guid itemId, CancellationToken ct)
    {
        var parent = await work.GetItemAsync(itemId, ct);
        var children = await items.ListAsync(x => x.ParentId == itemId && x.ProjectId == parent.Item.ProjectId, q => q.OrderBy(x => x.Number), cancellationToken: ct);
        var result = new List<WorkItemDto>();
        foreach (var child in children) result.Add((await work.GetItemAsync(child.Id, ct)).Item);
        return result;
    }

    private async Task<WorkProject> Access(Guid projectId, bool manage, CancellationToken ct)
    {
        if (!user.HasPermission(Permissions.WorkRead)) throw new UnauthorizedAccessException("Work access is required.");
        var project = await projects.GetByIdAsync(projectId, ct) ?? throw new KeyNotFoundException("Project not found.");
        if (!user.HasPermission(Permissions.WorkManage))
        {
            var member = await members.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.EmployeeId == user.EmployeeId, ct);
            if (member is null || (manage && (!user.HasPermission(Permissions.WorkAssign) || !member.CanAssignItems)))
                throw new UnauthorizedAccessException("Project planning requires assignment permission in this project.");
        }
        if (manage && !project.IsActive) throw new DomainException("This project is inactive.");
        return project;
    }

    private async Task<IReadOnlyList<SprintDto>> Map(IReadOnlyList<WorkSprint> rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.Id).ToArray();
        var all = await items.ListAsync(x => x.SprintId.HasValue && ids.Contains(x.SprintId.Value), cancellationToken: ct);
        return rows.Select(x =>
        {
            var scoped = all.Where(i => i.SprintId == x.Id).ToArray();
            var done = scoped.Where(i => i.Status == WorkItemStatus.Done).ToArray();
            return new SprintDto(x.Id, x.ProjectId, x.Name, x.Goal, x.StartsOn, x.EndsOn, x.Status,
                scoped.Length, done.Length, scoped.Sum(i => i.StoryPoints ?? 0), done.Sum(i => i.StoryPoints ?? 0), x.Version);
        }).ToArray();
    }
    private Task Record(WorkItem item, string field, string? before, string? after, CancellationToken ct) => history.AddAsync(new WorkItemHistory
    { TenantId = TenantId, WorkItemId = item.Id, ActorEmployeeId = user.EmployeeId, EventType = "planned", FieldName = field, BeforeValue = before, AfterValue = after }, ct);
}
