using System.Linq.Expressions;
using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record CreateWorkProjectRequest(string Key, string Name, string? Description, Guid? LeadEmployeeId);
public sealed record UpdateWorkProjectRequest(string Name, string? Description, Guid? LeadEmployeeId, bool IsActive, long Version);
public sealed record WorkProjectDto(Guid Id, string Key, string Name, string? Description, Guid? LeadEmployeeId, string? LeadName, bool IsActive, int MemberCount, long Version);
public sealed record SetWorkProjectMemberRequest(Guid EmployeeId, bool CanCreateItems, bool CanAssignItems, bool CanTransitionItems, bool CanLogWork, bool CanViewAllWorklogs);
public sealed record WorkProjectMemberDto(Guid Id, Guid EmployeeId, string EmployeeNumber, string EmployeeName, bool CanCreateItems, bool CanAssignItems, bool CanTransitionItems, bool CanLogWork, bool CanViewAllWorklogs);

public sealed record CreateWorkItemRequest(Guid ProjectId, WorkItemType Type, string Summary, string? Description, Guid? AssigneeEmployeeId,
    IReadOnlyList<Guid>? AssigneeEmployeeIds, Guid? ReporterEmployeeId, Guid? ParentId, WorkItemPriority Priority, DateOnly? DueDate,
    int? OriginalEstimateMinutes, decimal? StoryPoints, IReadOnlyList<string>? Labels);
public sealed record UpdateWorkItemRequest(WorkItemType Type, string Summary, string? Description, WorkItemPriority Priority, DateOnly? DueDate,
    int? OriginalEstimateMinutes, int? RemainingEstimateMinutes, decimal? StoryPoints, IReadOnlyList<string>? Labels,
    Guid? AssigneeEmployeeId, IReadOnlyList<Guid>? AssigneeEmployeeIds, Guid? ReporterEmployeeId, long Version);
public sealed record AssignWorkItemRequest(Guid? AssigneeEmployeeId, IReadOnlyList<Guid>? AssigneeEmployeeIds, long Version);
public sealed record TransitionWorkItemRequest(WorkItemStatus Status, WorkItemResolution? Resolution, string? Comment, long Version);
public sealed record WorkItemDto(Guid Id, Guid ProjectId, string ProjectKey, string Key, Guid? ParentId, WorkItemType Type, string Summary,
    WorkItemStatus Status, WorkItemPriority Priority, Guid? ReporterEmployeeId, string? ReporterName, Guid? AssigneeEmployeeId, string? AssigneeName,
    IReadOnlyList<Guid> AssigneeEmployeeIds, IReadOnlyList<string> AssigneeNames,
    DateOnly? DueDate, int? OriginalEstimateMinutes, int? RemainingEstimateMinutes, int LoggedMinutes, decimal? StoryPoints,
    IReadOnlyList<string> Labels, WorkItemResolution? Resolution, DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt, long Version, Guid? SprintId = null);
public sealed record WorkItemDetailDto(WorkItemDto Item, string? Description, IReadOnlyList<WorkCommentDto> Comments,
    IReadOnlyList<WorkLogDto> Worklogs, IReadOnlyList<WorkHistoryDto> History, WorkProjectMemberDto? Access);
public sealed record CreateWorkCommentRequest(string Body);
public sealed record UpdateWorkCommentRequest(string Body, long Version);
public sealed record WorkCommentDto(Guid Id, Guid? AuthorEmployeeId, string AuthorName, string Body, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, bool CanEdit, long Version);
public sealed record CreateWorkLogRequest(DateOnly WorkDate, int Minutes, string? Description, int? RemainingEstimateMinutes);
public sealed record UpdateWorkLogRequest(DateOnly WorkDate, int Minutes, string? Description, int? RemainingEstimateMinutes, long Version);
public sealed record WorkLogDto(Guid Id, Guid EmployeeId, string EmployeeName, DateOnly WorkDate, int Minutes, string? Description, DateTimeOffset CreatedAt, bool CanEdit, long Version);
public sealed record WorkHistoryDto(Guid Id, Guid? ActorEmployeeId, string ActorName, string EventType, string? FieldName, string? BeforeValue, string? AfterValue, DateTimeOffset CreatedAt);
public sealed record WorkTimeReportRow(Guid WorkItemId, string Key, string Summary, string? AssigneeName, IReadOnlyDictionary<string, int> MinutesByDate, int TotalMinutes);
public sealed record WorkTimeReportDto(DateOnly From, DateOnly To, IReadOnlyList<string> Dates, IReadOnlyList<WorkTimeReportRow> Rows, int TotalMinutes);
public sealed record WorkOverviewDto(int OpenItems, int DueSoon, int Overdue, int CompletedThisMonth, int LoggedMinutesThisMonth);

public interface IWorkManagementService
{
    Task<IReadOnlyList<WorkProjectDto>> ListProjectsAsync(CancellationToken ct);
    Task<WorkProjectDto> CreateProjectAsync(CreateWorkProjectRequest request, CancellationToken ct);
    Task<WorkProjectDto> UpdateProjectAsync(Guid id, UpdateWorkProjectRequest request, CancellationToken ct);
    Task<IReadOnlyList<WorkProjectMemberDto>> ListMembersAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<WorkProjectMemberDto>> SetMembersAsync(Guid projectId, IReadOnlyList<SetWorkProjectMemberRequest> requests, CancellationToken ct);
    Task<PagedResult<WorkItemDto>> SearchItemsAsync(PagedRequest page, Guid? projectId, WorkItemStatus? status, WorkItemPriority? priority, Guid? assigneeEmployeeId, CancellationToken ct, Guid? sprintId = null, bool backlogOnly = false, WorkItemType? type = null);
    Task<WorkItemDetailDto> GetItemAsync(Guid id, CancellationToken ct);
    Task<WorkItemDto> CreateItemAsync(CreateWorkItemRequest request, CancellationToken ct);
    Task<WorkItemDto> UpdateItemAsync(Guid id, UpdateWorkItemRequest request, CancellationToken ct);
    Task<WorkItemDto> AssignItemAsync(Guid id, AssignWorkItemRequest request, CancellationToken ct);
    Task<WorkItemDto> TransitionItemAsync(Guid id, TransitionWorkItemRequest request, CancellationToken ct);
    Task<WorkCommentDto> AddCommentAsync(Guid itemId, CreateWorkCommentRequest request, CancellationToken ct);
    Task<WorkCommentDto> UpdateCommentAsync(Guid itemId, Guid commentId, UpdateWorkCommentRequest request, CancellationToken ct);
    Task DeleteCommentAsync(Guid itemId, Guid commentId, CancellationToken ct);
    Task<WorkLogDto> AddWorklogAsync(Guid itemId, CreateWorkLogRequest request, CancellationToken ct);
    Task<WorkLogDto> UpdateWorklogAsync(Guid itemId, Guid worklogId, UpdateWorkLogRequest request, CancellationToken ct);
    Task DeleteWorklogAsync(Guid itemId, Guid worklogId, CancellationToken ct);
    Task<WorkTimeReportDto> GetTimeReportAsync(DateOnly from, DateOnly to, Guid? projectId, Guid? employeeId, CancellationToken ct);
    Task<WorkOverviewDto> GetOverviewAsync(CancellationToken ct);
}

public static class WorkWorkflow
{
    private static readonly IReadOnlyDictionary<WorkItemStatus, WorkItemStatus[]> Transitions = new Dictionary<WorkItemStatus, WorkItemStatus[]>
    {
        [WorkItemStatus.Backlog] = [WorkItemStatus.ToDo, WorkItemStatus.Cancelled],
        [WorkItemStatus.ToDo] = [WorkItemStatus.Backlog, WorkItemStatus.InProgress, WorkItemStatus.Cancelled],
        [WorkItemStatus.InProgress] = [WorkItemStatus.ToDo, WorkItemStatus.InReview, WorkItemStatus.Done, WorkItemStatus.Cancelled],
        [WorkItemStatus.InReview] = [WorkItemStatus.InProgress, WorkItemStatus.Done, WorkItemStatus.Cancelled],
        [WorkItemStatus.Done] = [WorkItemStatus.InProgress],
        [WorkItemStatus.Cancelled] = [WorkItemStatus.Backlog]
    };

    public static bool CanTransition(WorkItemStatus from, WorkItemStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && Enum.IsDefined(to) && (from == to || allowed.Contains(to));
}

public sealed class WorkManagementService(
    IRepository<WorkProject> projects, IRepository<WorkProjectMember> members, IRepository<WorkItem> items,
    IRepository<WorkItemAssignee> assignees, IRepository<WorkItemComment> comments, IRepository<WorkLog> worklogs, IRepository<WorkItemHistory> history,
    IRepository<Employee> employees, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork unitOfWork,
    INotificationService notifications) : ServiceBase(tenant), IWorkManagementService
{
    public async Task<IReadOnlyList<WorkProjectDto>> ListProjectsAsync(CancellationToken ct)
    {
        var allowed = await AllowedProjectIdsAsync(ct);
        var rows = await projects.ListAsync(x => allowed.Contains(x.Id), x => x.OrderBy(p => p.Name), cancellationToken: ct);
        return await MapProjectsAsync(rows, ct);
    }

    public async Task<WorkProjectDto> CreateProjectAsync(CreateWorkProjectRequest request, CancellationToken ct)
    {
        RequirePermission(Permissions.WorkManage);
        var key = NormalizeProjectKey(request.Key);
        Required(request.Name, "Project name");
        if (await projects.AnyAsync(x => x.Key == key, ct)) throw new DomainException("Project key is already in use.");
        if (request.LeadEmployeeId.HasValue) await RequireEmployeeAsync(request.LeadEmployeeId.Value, ct);
        var project = new WorkProject
        {
            TenantId = TenantId, Key = key, Name = request.Name.Trim(),
            Description = Clean(request.Description), LeadEmployeeId = request.LeadEmployeeId
        };
        await projects.AddAsync(project, ct);
        if (request.LeadEmployeeId.HasValue)
            await members.AddAsync(new WorkProjectMember
            {
                TenantId = TenantId, ProjectId = project.Id, EmployeeId = request.LeadEmployeeId.Value,
                CanCreateItems = true, CanAssignItems = true, CanTransitionItems = true,
                CanLogWork = true, CanViewAllWorklogs = true
            }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapProjectsAsync([project], ct))[0];
    }

    public async Task<WorkProjectDto> UpdateProjectAsync(Guid id, UpdateWorkProjectRequest request, CancellationToken ct)
    {
        RequirePermission(Permissions.WorkManage);
        var project = await RequireProjectAsync(id, ct);
        CheckVersion(project, request.Version);
        Required(request.Name, "Project name");
        if (request.LeadEmployeeId.HasValue) await RequireEmployeeAsync(request.LeadEmployeeId.Value, ct);
        project.Name = request.Name.Trim();
        project.Description = Clean(request.Description);
        project.LeadEmployeeId = request.LeadEmployeeId;
        project.IsActive = request.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapProjectsAsync([project], ct))[0];
    }

    public async Task<IReadOnlyList<WorkProjectMemberDto>> ListMembersAsync(Guid projectId, CancellationToken ct)
    {
        await RequireProjectAccessAsync(projectId, AccessKind.Read, ct);
        var rows = await members.ListAsync(x => x.ProjectId == projectId, x => x.OrderBy(m => m.EmployeeId), cancellationToken: ct);
        return await MapMembersAsync(rows, ct);
    }

    public async Task<IReadOnlyList<WorkProjectMemberDto>> SetMembersAsync(Guid projectId, IReadOnlyList<SetWorkProjectMemberRequest> requests, CancellationToken ct)
    {
        RequirePermission(Permissions.WorkManage);
        var project = await RequireProjectAsync(projectId, ct);
        if (requests.Select(x => x.EmployeeId).Distinct().Count() != requests.Count)
            throw new DomainException("Each employee can appear only once.");
        foreach (var request in requests) await RequireEmployeeAsync(request.EmployeeId, ct);
        var existing = await members.ListAsync(x => x.ProjectId == projectId, cancellationToken: ct);
        var existingEmployeeIds = existing.Select(x => x.EmployeeId).ToHashSet();
        var changedEmployeeIds = requests.Where(request => existing.FirstOrDefault(x => x.EmployeeId == request.EmployeeId) is { } row
            && (row.CanCreateItems != request.CanCreateItems || row.CanAssignItems != request.CanAssignItems
                || row.CanTransitionItems != request.CanTransitionItems || row.CanLogWork != request.CanLogWork
                || row.CanViewAllWorklogs != request.CanViewAllWorklogs))
            .Select(x => x.EmployeeId).ToArray();
        var requestedEmployeeIds = requests.Select(x => x.EmployeeId).ToHashSet();
        foreach (var row in existing.Where(x => !requestedEmployeeIds.Contains(x.EmployeeId))) members.Remove(row);
        var existingByEmployeeId = existing.ToDictionary(x => x.EmployeeId);
        foreach (var request in requests)
        {
            if (!existingByEmployeeId.TryGetValue(request.EmployeeId, out var row))
            {
                row = new WorkProjectMember
                {
                    TenantId = TenantId, ProjectId = projectId, EmployeeId = request.EmployeeId
                };
                await members.AddAsync(row, ct);
            }
            row.CanCreateItems = request.CanCreateItems;
            row.CanAssignItems = request.CanAssignItems;
            row.CanTransitionItems = request.CanTransitionItems;
            row.CanLogWork = request.CanLogWork;
            row.CanViewAllWorklogs = request.CanViewAllWorklogs;
        }
        await notifications.QueueForEmployeesAsync(requestedEmployeeIds.Where(x => !existingEmployeeIds.Contains(x)).Select(x => (Guid?)x),
            "Project access granted", $"You were granted access to {project.Name}.", "work", "/work", ct);
        await notifications.QueueForEmployeesAsync(changedEmployeeIds.Select(x => (Guid?)x),
            "Project access updated", $"Your access permissions for {project.Name} were updated.", "work", "/work", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await ListMembersAsync(projectId, ct);
    }

    public async Task<PagedResult<WorkItemDto>> SearchItemsAsync(PagedRequest page, Guid? projectId, WorkItemStatus? status, WorkItemPriority? priority, Guid? assigneeEmployeeId, CancellationToken ct, Guid? sprintId = null, bool backlogOnly = false, WorkItemType? type = null)
    {
        var allowed = await AllowedProjectIdsAsync(ct);
        var query = page.Search?.Trim().ToLowerInvariant();
        var assignedItemIds = assigneeEmployeeId.HasValue
            ? (await assignees.ListAsync(x => x.EmployeeId == assigneeEmployeeId.Value, cancellationToken: ct)).Select(x => x.WorkItemId).ToHashSet()
            : [];
        Expression<Func<WorkItem, bool>> predicate = x => allowed.Contains(x.ProjectId)
            && (!sprintId.HasValue || x.SprintId == sprintId)
            && (!backlogOnly || x.SprintId == null)
            && (!type.HasValue || x.Type == type)
            && (!projectId.HasValue || x.ProjectId == projectId.Value)
            && (!status.HasValue || x.Status == status.Value)
            && (!priority.HasValue || x.Priority == priority.Value)
            && (!assigneeEmployeeId.HasValue || x.AssigneeEmployeeId == assigneeEmployeeId.Value || assignedItemIds.Contains(x.Id))
            && (string.IsNullOrEmpty(query) || x.Key.ToLower().Contains(query) || x.Summary.ToLower().Contains(query));
        var total = await items.CountAsync(predicate, ct);
        var rows = await items.ListAsync(predicate, x => x.OrderByDescending(i => i.CreatedAt), page.Skip, page.SafePageSize, ct);
        return new PagedResult<WorkItemDto>(await MapItemsAsync(rows, ct), page.SafePage, page.SafePageSize, total);
    }

    public async Task<WorkItemDetailDto> GetItemAsync(Guid id, CancellationToken ct)
    {
        var item = await RequireItemAsync(id, ct);
        var access = await RequireProjectAccessAsync(item.ProjectId, AccessKind.Read, ct);
        var commentRows = await comments.ListAsync(x => x.WorkItemId == id, x => x.OrderBy(c => c.CreatedAt), cancellationToken: ct);
        var logRows = await worklogs.ListAsync(x => x.WorkItemId == id, x => x.OrderByDescending(w => w.WorkDate).ThenByDescending(w => w.CreatedAt), cancellationToken: ct);
        if (!CanViewAllLogs(access)) logRows = logRows.Where(x => x.EmployeeId == user.EmployeeId).ToArray();
        var historyRows = await history.ListAsync(x => x.WorkItemId == id, x => x.OrderByDescending(h => h.CreatedAt), take: 100, cancellationToken: ct);
        var mappedItem = (await MapItemsAsync([item], ct))[0];
        return new WorkItemDetailDto(mappedItem, item.Description, await MapCommentsAsync(commentRows, ct),
            await MapWorklogsAsync(logRows, ct), await MapHistoryAsync(historyRows, ct),
            access is null ? null : (await MapMembersAsync([access], ct))[0]);
    }

    public async Task<WorkItemDto> CreateItemAsync(CreateWorkItemRequest request, CancellationToken ct)
    {
        var access = await RequireProjectAccessAsync(request.ProjectId, AccessKind.Create, ct);
        var project = await RequireProjectAsync(request.ProjectId, ct);
        if (!project.IsActive) throw new DomainException("This project is inactive.");
        Required(request.Summary, "Summary");
        ValidateItemEnums(request.Type, request.Priority);
        var assigneeIds = NormalizeAssigneeIds(request.AssigneeEmployeeId, request.AssigneeEmployeeIds);
        foreach (var assigneeId in assigneeIds)
            await RequireAssignableAsync(request.ProjectId, assigneeId, access, ct);
        var reporterId = request.ReporterEmployeeId ?? user.EmployeeId;
        if (reporterId.HasValue) await RequireEmployeeAsync(reporterId.Value, ct);
        if (request.ParentId.HasValue)
        {
            var parent = await RequireItemAsync(request.ParentId.Value, ct);
            if (parent.ProjectId != request.ProjectId)
                throw new DomainException("Parent tickets and subtasks must belong to the same project.");
            if (parent.Status is WorkItemStatus.Done or WorkItemStatus.Cancelled)
                throw new DomainException("Reopen the parent before adding child work.");
            if (parent.Type == WorkItemType.Subtask || request.Type == WorkItemType.Epic)
                throw new DomainException("Epics cannot be children and subtasks cannot contain child work.");
        }
        else if (request.Type == WorkItemType.Subtask) throw new DomainException("A subtask requires a parent ticket.");
        ValidateEstimate(request.OriginalEstimateMinutes, request.StoryPoints);
        var number = project.NextItemNumber++;
        var item = new WorkItem
        {
            TenantId = TenantId, ProjectId = project.Id, Number = number, Key = $"{project.Key}-{number}",
            ParentId = request.ParentId, Type = request.Type, Summary = request.Summary.Trim(),
            Description = Clean(request.Description), Priority = request.Priority, ReporterEmployeeId = reporterId,
            AssigneeEmployeeId = assigneeIds.Count > 0 ? assigneeIds[0] : null, DueDate = request.DueDate,
            OriginalEstimateMinutes = request.OriginalEstimateMinutes, RemainingEstimateMinutes = request.OriginalEstimateMinutes,
            StoryPoints = request.StoryPoints, LabelsCsv = NormalizeLabels(request.Labels)
        };
        await items.AddAsync(item, ct);
        await SetAssigneesAsync(item, assigneeIds, ct);
        await AddHistoryAsync(item.Id, "created", null, null, item.Key, ct);
        await NotifyAsync(item, "Ticket created", $"{item.Key} was created and requires your attention.",
            assigneeIds.Select(x => (Guid?)x).Append(item.ReporterEmployeeId), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapItemsAsync([item], ct))[0];
    }

    public async Task<WorkItemDto> UpdateItemAsync(Guid id, UpdateWorkItemRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(id, ct);
        await RequireProjectAccessAsync(item.ProjectId, AccessKind.Create, ct);
        CheckVersion(item, request.Version);
        Required(request.Summary, "Summary");
        ValidateItemEnums(request.Type, request.Priority);
        ValidateEstimate(request.OriginalEstimateMinutes, request.StoryPoints);
        var currentAssigneeIds = await CurrentAssigneeIdsAsync(item, ct);
        NonNegative(request.RemainingEstimateMinutes, "Remaining estimate");
        var assigneeIds = request.AssigneeEmployeeIds is null && !request.AssigneeEmployeeId.HasValue
            ? currentAssigneeIds
            : NormalizeAssigneeIds(request.AssigneeEmployeeId, request.AssigneeEmployeeIds);
        var reporterId = request.ReporterEmployeeId;
        var assigneesChanged = !currentAssigneeIds.SequenceEqual(assigneeIds);
        if (assigneesChanged)
        {
            var assignAccess = await RequireProjectAccessAsync(item.ProjectId, AccessKind.Assign, ct);
            foreach (var assigneeId in assigneeIds)
                await RequireAssignableAsync(item.ProjectId, assigneeId, assignAccess, ct);
        }
        if (reporterId.HasValue && reporterId != item.ReporterEmployeeId)
            await RequireEmployeeAsync(reporterId.Value, ct);
        var before = item.Summary;
        item.Type = request.Type;
        item.Summary = request.Summary.Trim();
        item.Description = Clean(request.Description);
        item.Priority = request.Priority;
        item.ReporterEmployeeId = reporterId;
        item.AssigneeEmployeeId = assigneeIds.Count > 0 ? assigneeIds[0] : null;
        item.DueDate = request.DueDate;
        item.OriginalEstimateMinutes = request.OriginalEstimateMinutes;
        item.RemainingEstimateMinutes = request.RemainingEstimateMinutes;
        item.StoryPoints = request.StoryPoints;
        item.LabelsCsv = NormalizeLabels(request.Labels);
        await SetAssigneesAsync(item, assigneeIds, ct);
        await AddHistoryAsync(id, "updated", "details", before, item.Summary, ct);
        await NotifyAsync(item, "Ticket updated", $"{item.Key} details were updated.", assigneeIds.Select(x => (Guid?)x).Append(item.ReporterEmployeeId), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapItemsAsync([item], ct))[0];
    }

    public async Task<WorkItemDto> AssignItemAsync(Guid id, AssignWorkItemRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(id, ct);
        var access = await RequireProjectAccessAsync(item.ProjectId, AccessKind.Assign, ct);
        CheckVersion(item, request.Version);
        var assigneeIds = NormalizeAssigneeIds(request.AssigneeEmployeeId, request.AssigneeEmployeeIds);
        foreach (var assigneeId in assigneeIds)
            await RequireAssignableAsync(item.ProjectId, assigneeId, access, ct);
        var before = string.Join(',', (await CurrentAssigneeIdsAsync(item, ct)).Select(x => x.ToString()));
        item.AssigneeEmployeeId = assigneeIds.Count > 0 ? assigneeIds[0] : null;
        await SetAssigneesAsync(item, assigneeIds, ct);
        await AddHistoryAsync(id, "assigned", "assignee", before, string.Join(',', assigneeIds), ct);
        await NotifyAsync(item, "Ticket assigned", $"{item.Key} was assigned to you.", assigneeIds.Select(x => (Guid?)x), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapItemsAsync([item], ct))[0];
    }

    public async Task<WorkItemDto> TransitionItemAsync(Guid id, TransitionWorkItemRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(id, ct);
        await RequireProjectAccessAsync(item.ProjectId, AccessKind.Transition, ct);
        CheckVersion(item, request.Version);
        if (!WorkWorkflow.CanTransition(item.Status, request.Status))
            throw new DomainException($"Tickets cannot move directly from {item.Status} to {request.Status}.");
        if (request.Resolution.HasValue && !Enum.IsDefined(request.Resolution.Value))
            throw new DomainException("Resolution is invalid.");
        if (request.Status == WorkItemStatus.Done && await items.AnyAsync(x => x.ParentId == id && x.Status != WorkItemStatus.Done && x.Status != WorkItemStatus.Cancelled, ct))
            throw new DomainException("Complete or cancel all subtasks before completing the parent ticket.");
        if (item.Status == request.Status) return (await MapItemsAsync([item], ct))[0];
        var before = item.Status;
        item.Status = request.Status;
        if (request.Status is WorkItemStatus.Done or WorkItemStatus.Cancelled)
        {
            item.Resolution = request.Resolution ?? (request.Status == WorkItemStatus.Done ? WorkItemResolution.Done : WorkItemResolution.Cancelled);
            item.ResolvedAt = DateTimeOffset.UtcNow;
            item.RemainingEstimateMinutes = 0;
        }
        else
        {
            item.Resolution = null;
            item.ResolvedAt = null;
        }
        await AddHistoryAsync(id, "transitioned", "status", before.ToString(), request.Status.ToString(), ct);
        if (!string.IsNullOrWhiteSpace(request.Comment)) await AddCommentInternalAsync(id, request.Comment, ct);
        await NotifyAsync(item, "Ticket status changed", $"{item.Key} moved from {before} to {request.Status}.",
            (await CurrentAssigneeIdsAsync(item, ct)).Select(x => (Guid?)x).Append(item.ReporterEmployeeId), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapItemsAsync([item], ct))[0];
    }

    public async Task<WorkCommentDto> AddCommentAsync(Guid itemId, CreateWorkCommentRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(itemId, ct);
        await RequireProjectAccessAsync(item.ProjectId, AccessKind.Comment, ct);
        var row = await AddCommentInternalAsync(itemId, request.Body, ct);
        await NotifyAsync(item, "New ticket comment", $"A comment was added to {item.Key}.",
            (await CurrentAssigneeIdsAsync(item, ct)).Select(x => (Guid?)x).Append(item.ReporterEmployeeId), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapCommentsAsync([row], ct))[0];
    }

    public async Task<WorkCommentDto> UpdateCommentAsync(Guid itemId, Guid commentId, UpdateWorkCommentRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(itemId, ct);
        await RequireProjectAccessAsync(item.ProjectId, AccessKind.Comment, ct);
        var row = await comments.FirstOrDefaultAsync(x => x.Id == commentId && x.WorkItemId == itemId, ct)
            ?? throw new DomainException("Comment was not found.");
        if (!CanManageOwn(row.AuthorEmployeeId)) throw new DomainException("You can only edit your own comments.");
        CheckVersion(row, request.Version);
        Required(request.Body, "Comment");
        row.Body = request.Body.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapCommentsAsync([row], ct))[0];
    }

    public async Task DeleteCommentAsync(Guid itemId, Guid commentId, CancellationToken ct)
    {
        var item = await RequireItemAsync(itemId, ct);
        await RequireProjectAccessAsync(item.ProjectId, AccessKind.Comment, ct);
        var row = await comments.FirstOrDefaultAsync(x => x.Id == commentId && x.WorkItemId == itemId, ct)
            ?? throw new DomainException("Comment was not found.");
        if (!CanManageOwn(row.AuthorEmployeeId)) throw new DomainException("You can only delete your own comments.");
        comments.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<WorkLogDto> AddWorklogAsync(Guid itemId, CreateWorkLogRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(itemId, ct);
        await RequireProjectAccessAsync(item.ProjectId, AccessKind.Log, ct);
        ValidateWorklog(request.WorkDate, request.Minutes);
        var employeeId = user.EmployeeId ?? throw new DomainException("An employee profile is required to log work.");
        await ValidateDailyWorklogTotalAsync(employeeId, request.WorkDate, request.Minutes, null, ct);
        var row = new WorkLog
        {
            TenantId = TenantId, WorkItemId = itemId, EmployeeId = employeeId,
            WorkDate = request.WorkDate, Minutes = request.Minutes, Description = Clean(request.Description)
        };
        if (request.RemainingEstimateMinutes.HasValue)
            item.RemainingEstimateMinutes = NonNegative(request.RemainingEstimateMinutes, "Remaining estimate");
        else if (item.RemainingEstimateMinutes.HasValue)
            item.RemainingEstimateMinutes = Math.Max(0, item.RemainingEstimateMinutes.Value - request.Minutes);
        await worklogs.AddAsync(row, ct);
        await AddHistoryAsync(itemId, "work_logged", "time", null, request.Minutes.ToString(), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapWorklogsAsync([row], ct))[0];
    }

    public async Task<WorkLogDto> UpdateWorklogAsync(Guid itemId, Guid worklogId, UpdateWorkLogRequest request, CancellationToken ct)
    {
        var item = await RequireItemAsync(itemId, ct);
        var access = await RequireProjectAccessAsync(item.ProjectId, AccessKind.Log, ct);
        ValidateWorklog(request.WorkDate, request.Minutes);
        var row = await worklogs.FirstOrDefaultAsync(x => x.Id == worklogId && x.WorkItemId == itemId, ct)
            ?? throw new DomainException("Worklog was not found.");
        if (!CanManageOwn(row.EmployeeId))
            throw new DomainException("You can only edit your own worklogs.");
        CheckVersion(row, request.Version);
        await ValidateDailyWorklogTotalAsync(row.EmployeeId, request.WorkDate, request.Minutes, row.Id, ct);
        NonNegative(request.RemainingEstimateMinutes, "Remaining estimate");
        row.WorkDate = request.WorkDate;
        row.Minutes = request.Minutes;
        row.Description = Clean(request.Description);
        if (request.RemainingEstimateMinutes.HasValue)
            item.RemainingEstimateMinutes = NonNegative(request.RemainingEstimateMinutes, "Remaining estimate");
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapWorklogsAsync([row], ct))[0];
    }

    public async Task DeleteWorklogAsync(Guid itemId, Guid worklogId, CancellationToken ct)
    {
        var item = await RequireItemAsync(itemId, ct);
        var access = await RequireProjectAccessAsync(item.ProjectId, AccessKind.Log, ct);
        var row = await worklogs.FirstOrDefaultAsync(x => x.Id == worklogId && x.WorkItemId == itemId, ct)
            ?? throw new DomainException("Worklog was not found.");
        if (!CanManageOwn(row.EmployeeId))
            throw new DomainException("You can only delete your own worklogs.");
        worklogs.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<WorkTimeReportDto> GetTimeReportAsync(DateOnly from, DateOnly to, Guid? projectId, Guid? employeeId, CancellationToken ct)
    {
        if (to < from) throw new DomainException("Report end date must be on or after the start date.");
        if (to.DayNumber - from.DayNumber > 366) throw new DomainException("A time report can cover at most 367 days.");
        var allowed = await AllowedProjectIdsAsync(ct);
        if (projectId.HasValue && !allowed.Contains(projectId.Value))
            throw new DomainException("You do not have access to this project.");
        var canManage = user.HasPermission(Permissions.WorkManage);
        var currentEmployeeId = user.EmployeeId;
        if (!canManage && !currentEmployeeId.HasValue)
            throw new DomainException("An employee profile is required to view worklogs.");
        var memberAccess = canManage
            ? Array.Empty<WorkProjectMember>()
            : (await members.ListAsync(x => x.EmployeeId == currentEmployeeId!.Value, cancellationToken: ct)).ToArray();
        var viewAllProjects = user.HasPermission(Permissions.WorkViewAllLogs)
            ? memberAccess.Where(x => x.CanViewAllWorklogs).Select(x => x.ProjectId).ToHashSet()
            : [];
        var projectIds = projectId.HasValue ? new[] { projectId.Value } : allowed.ToArray();
        var itemRows = await items.ListAsync(x => projectIds.Contains(x.ProjectId), x => x.OrderBy(i => i.Key), cancellationToken: ct);
        var itemIds = itemRows.Select(x => x.Id).ToArray();
        var logRows = (await worklogs.ListAsync(
            x => itemIds.Contains(x.WorkItemId) && x.WorkDate >= from && x.WorkDate <= to,
            cancellationToken: ct)).ToArray();
        if (!canManage)
        {
            var itemProjects = itemRows.ToDictionary(x => x.Id, x => x.ProjectId);
            logRows = logRows.Where(x => x.EmployeeId == currentEmployeeId
                || viewAllProjects.Contains(itemProjects[x.WorkItemId])).ToArray();
        }
        if (employeeId.HasValue) logRows = logRows.Where(x => x.EmployeeId == employeeId.Value).ToArray();
        var dates = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => from.AddDays(offset).ToString("yyyy-MM-dd")).ToArray();
        var rows = itemRows.Where(i => logRows.Any(w => w.WorkItemId == i.Id)).Select(item =>
        {
            var itemLogs = logRows.Where(x => x.WorkItemId == item.Id).ToArray();
            var cells = dates.ToDictionary(date => date,
                date => itemLogs.Where(x => x.WorkDate.ToString("yyyy-MM-dd") == date).Sum(x => x.Minutes));
            return new WorkTimeReportRow(item.Id, item.Key, item.Summary, null, cells, itemLogs.Sum(x => x.Minutes));
        }).ToArray();
        var reportAssigneeMap = (await assignees.ListAsync(x => itemIds.Contains(x.WorkItemId), cancellationToken: ct))
            .GroupBy(x => x.WorkItemId)
            .ToDictionary(x => x.Key, x => x.Select(a => a.EmployeeId).Distinct().ToArray());
        var people = await EmployeeNamesAsync(ct);
        rows = rows.Select(row =>
        {
            var item = itemRows.First(x => x.Id == row.WorkItemId);
            var ids = reportAssigneeMap.GetValueOrDefault(item.Id) ?? [];
            if (item.AssigneeEmployeeId.HasValue && !ids.Contains(item.AssigneeEmployeeId.Value)) ids = [item.AssigneeEmployeeId.Value, .. ids];
            var assigned = ids.Select(x => Name(people, x)).Where(x => !string.IsNullOrWhiteSpace(x));
            return row with { AssigneeName = string.Join(", ", assigned.DefaultIfEmpty(Name(people, item.AssigneeEmployeeId) ?? "Unassigned")) };
        }).ToArray();
        return new WorkTimeReportDto(from, to, dates, rows, rows.Sum(x => x.TotalMinutes));
    }

    public async Task<WorkOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        var allowed = await AllowedProjectIdsAsync(ct);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var soon = now.AddDays(7);
        var rows = await items.ListAsync(x => allowed.Contains(x.ProjectId), cancellationToken: ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var logs = (await worklogs.ListAsync(x => ids.Contains(x.WorkItemId) && x.WorkDate >= monthStart,
            cancellationToken: ct)).ToArray();
        if (!user.HasPermission(Permissions.WorkManage))
        {
            var employeeId = user.EmployeeId ?? throw new DomainException("An employee profile is required for work access.");
            var viewAllProjects = user.HasPermission(Permissions.WorkViewAllLogs)
                ? (await members.ListAsync(x => x.EmployeeId == employeeId && x.CanViewAllWorklogs, cancellationToken: ct))
                    .Select(x => x.ProjectId).ToHashSet()
                : [];
            var itemProjects = rows.ToDictionary(x => x.Id, x => x.ProjectId);
            logs = logs.Where(x => x.EmployeeId == employeeId || viewAllProjects.Contains(itemProjects[x.WorkItemId])).ToArray();
        }
        return new WorkOverviewDto(
            rows.Count(x => x.Status is not WorkItemStatus.Done and not WorkItemStatus.Cancelled),
            rows.Count(x => x.DueDate >= now && x.DueDate <= soon && x.Status is not WorkItemStatus.Done and not WorkItemStatus.Cancelled),
            rows.Count(x => x.DueDate < now && x.Status is not WorkItemStatus.Done and not WorkItemStatus.Cancelled),
            rows.Count(x => x.Status == WorkItemStatus.Done
                && x.ResolvedAt >= new DateTimeOffset(monthStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
            logs.Sum(x => x.Minutes));
    }

    private async Task<IReadOnlyList<Guid>> AllowedProjectIdsAsync(CancellationToken ct)
    {
        RequirePermission(Permissions.WorkRead);
        if (user.HasPermission(Permissions.WorkManage))
            return (await projects.ListAsync(cancellationToken: ct)).Select(x => x.Id).ToArray();
        var employeeId = user.EmployeeId ?? throw new DomainException("An employee profile is required for work access.");
        return (await members.ListAsync(x => x.EmployeeId == employeeId, cancellationToken: ct))
            .Select(x => x.ProjectId).ToArray();
    }

    private async Task<WorkProjectMember?> RequireProjectAccessAsync(Guid projectId, AccessKind kind, CancellationToken ct)
    {
        RequirePermission(Permissions.WorkRead);
        var project = await RequireProjectAsync(projectId, ct);
        if (kind != AccessKind.Read && !project.IsActive)
            throw new DomainException("This project is inactive. Reactivate it before changing tickets.");
        if (user.HasPermission(Permissions.WorkManage)) return null;
        var employeeId = user.EmployeeId ?? throw new DomainException("An employee profile is required for work access.");
        var access = await members.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.EmployeeId == employeeId, ct)
            ?? throw new DomainException("You do not have access to this project.");
        var permitted = kind switch
        {
            AccessKind.Read => user.HasPermission(Permissions.WorkRead),
            AccessKind.Create => user.HasPermission(Permissions.WorkCreate) && access.CanCreateItems,
            AccessKind.Assign => user.HasPermission(Permissions.WorkAssign) && access.CanAssignItems,
            AccessKind.Transition => user.HasPermission(Permissions.WorkTransition) && access.CanTransitionItems,
            AccessKind.Comment => user.HasPermission(Permissions.WorkComment),
            AccessKind.Log => user.HasPermission(Permissions.WorkLog) && access.CanLogWork,
            _ => false
        };
        if (!permitted) throw new DomainException("You do not have permission to perform this action in the project.");
        return access;
    }

    private async Task RequireAssignableAsync(Guid projectId, Guid employeeId, WorkProjectMember? actorAccess, CancellationToken ct)
    {
        await RequireEmployeeAsync(employeeId, ct);
        if (!user.HasPermission(Permissions.WorkManage)
            && (actorAccess is null || !actorAccess.CanAssignItems || !user.HasPermission(Permissions.WorkAssign)))
            throw new DomainException("You do not have permission to assign tickets.");
        if (!await members.AnyAsync(x => x.ProjectId == projectId && x.EmployeeId == employeeId, ct))
            throw new DomainException("The assignee is not a member of this project.");
    }

    private async Task<WorkProject> RequireProjectAsync(Guid id, CancellationToken ct) =>
        await projects.GetByIdAsync(id, ct) ?? throw new DomainException("Project was not found.");

    private async Task<WorkItem> RequireItemAsync(Guid id, CancellationToken ct) =>
        await items.GetByIdAsync(id, ct) ?? throw new DomainException("Ticket was not found.");

    private async Task<Employee> RequireEmployeeAsync(Guid id, CancellationToken ct) =>
        await employees.FirstOrDefaultAsync(x => x.Id == id && (x.Status == EmploymentStatus.Active
            || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod), ct)
        ?? throw new DomainException("Employee was not found or is inactive.");

    private void RequirePermission(string permission)
    {
        if (!user.HasPermission(permission)) throw new DomainException("You do not have permission to perform this action.");
    }

    private bool CanViewAllLogs(WorkProjectMember? access) =>
        user.HasPermission(Permissions.WorkManage)
        || (user.HasPermission(Permissions.WorkViewAllLogs) && access?.CanViewAllWorklogs == true);

    private bool CanManageOwn(Guid? employeeId) =>
        user.HasPermission(Permissions.WorkManage) || (employeeId.HasValue && employeeId == user.EmployeeId);

    private async Task<WorkItemComment> AddCommentInternalAsync(Guid itemId, string body, CancellationToken ct)
    {
        Required(body, "Comment");
        var row = new WorkItemComment
        {
            TenantId = TenantId, WorkItemId = itemId, AuthorEmployeeId = user.EmployeeId, Body = body.Trim()
        };
        await comments.AddAsync(row, ct);
        await AddHistoryAsync(itemId, "commented", null, null, null, ct);
        return row;
    }

    private async Task<IReadOnlyList<Guid>> CurrentAssigneeIdsAsync(WorkItem item, CancellationToken ct)
    {
        var rows = await assignees.ListAsync(x => x.WorkItemId == item.Id, x => x.OrderBy(a => a.CreatedAt), cancellationToken: ct);
        var ids = rows.Select(x => x.EmployeeId).ToList();
        if (item.AssigneeEmployeeId.HasValue && !ids.Contains(item.AssigneeEmployeeId.Value)) ids.Insert(0, item.AssigneeEmployeeId.Value);
        return ids.Distinct().ToArray();
    }

    private async Task SetAssigneesAsync(WorkItem item, IReadOnlyList<Guid> employeeIds, CancellationToken ct)
    {
        var existing = await assignees.ListAsync(x => x.WorkItemId == item.Id, cancellationToken: ct);
        foreach (var row in existing.Where(x => !employeeIds.Contains(x.EmployeeId))) assignees.Remove(row);
        var current = existing.Select(x => x.EmployeeId).ToHashSet();
        foreach (var employeeId in employeeIds.Where(x => !current.Contains(x)))
            await assignees.AddAsync(new WorkItemAssignee { TenantId = TenantId, WorkItemId = item.Id, EmployeeId = employeeId }, ct);
    }

    private async Task AddHistoryAsync(Guid itemId, string eventType, string? field, string? before, string? after, CancellationToken ct) =>
        await history.AddAsync(new WorkItemHistory
        {
            TenantId = TenantId, WorkItemId = itemId, ActorEmployeeId = user.EmployeeId,
            EventType = eventType, FieldName = field, BeforeValue = before, AfterValue = after
        }, ct);

    private Task NotifyAsync(WorkItem item, string title, string message, IEnumerable<Guid?> recipients, CancellationToken ct) =>
        notifications.QueueForEmployeesAsync(recipients, title, message, "work", "/work", ct);

    private async Task<IReadOnlyList<WorkProjectDto>> MapProjectsAsync(IReadOnlyList<WorkProject> rows, CancellationToken ct)
    {
        var people = await EmployeeNamesAsync(ct);
        var ids = rows.Select(r => r.Id).ToArray();
        var counts = (await members.ListAsync(x => ids.Contains(x.ProjectId), cancellationToken: ct))
            .GroupBy(x => x.ProjectId).ToDictionary(x => x.Key, x => x.Count());
        return rows.Select(x => new WorkProjectDto(x.Id, x.Key, x.Name, x.Description, x.LeadEmployeeId,
            Name(people, x.LeadEmployeeId), x.IsActive, counts.GetValueOrDefault(x.Id), x.Version)).ToArray();
    }

    private async Task<IReadOnlyList<WorkProjectMemberDto>> MapMembersAsync(IReadOnlyList<WorkProjectMember> rows, CancellationToken ct)
    {
        var people = await employees.ListAsync(cancellationToken: ct);
        var map = people.ToDictionary(x => x.Id);
        return rows.Where(x => map.ContainsKey(x.EmployeeId)).Select(x =>
        {
            var employee = map[x.EmployeeId];
            return new WorkProjectMemberDto(x.Id, x.EmployeeId, employee.EmployeeNumber, employee.FullName,
                x.CanCreateItems, x.CanAssignItems, x.CanTransitionItems, x.CanLogWork, x.CanViewAllWorklogs);
        }).OrderBy(x => x.EmployeeName).ToArray();
    }

    private async Task<IReadOnlyList<WorkItemDto>> MapItemsAsync(IReadOnlyList<WorkItem> rows, CancellationToken ct)
    {
        var people = await EmployeeNamesAsync(ct);
        var projectMap = (await projects.ListAsync(cancellationToken: ct)).ToDictionary(x => x.Id);
        var ids = rows.Select(x => x.Id).ToArray();
        var totals = (await worklogs.ListAsync(x => ids.Contains(x.WorkItemId), cancellationToken: ct))
            .GroupBy(x => x.WorkItemId).ToDictionary(x => x.Key, x => x.Sum(w => w.Minutes));
        var assigneeMap = (await assignees.ListAsync(x => ids.Contains(x.WorkItemId), cancellationToken: ct))
            .GroupBy(x => x.WorkItemId)
            .ToDictionary(x => x.Key, x => x.Select(a => a.EmployeeId).Distinct().ToArray());
        return rows.Select(x =>
        {
            var assignedIds = assigneeMap.GetValueOrDefault(x.Id) ?? [];
            if (x.AssigneeEmployeeId.HasValue && !assignedIds.Contains(x.AssigneeEmployeeId.Value))
                assignedIds = [x.AssigneeEmployeeId.Value, .. assignedIds];
            var assigneeNames = assignedIds.Select(id => Name(people, id)).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray();
            return new WorkItemDto(
                x.Id, x.ProjectId, projectMap.GetValueOrDefault(x.ProjectId)?.Key ?? string.Empty, x.Key, x.ParentId,
                x.Type, x.Summary, x.Status, x.Priority, x.ReporterEmployeeId, Name(people, x.ReporterEmployeeId),
                x.AssigneeEmployeeId, Name(people, x.AssigneeEmployeeId), assignedIds, assigneeNames, x.DueDate, x.OriginalEstimateMinutes,
                x.RemainingEstimateMinutes, totals.GetValueOrDefault(x.Id), x.StoryPoints, SplitLabels(x.LabelsCsv),
                x.Resolution, x.ResolvedAt, x.CreatedAt, x.Version, x.SprintId);
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorkCommentDto>> MapCommentsAsync(IReadOnlyList<WorkItemComment> rows, CancellationToken ct)
    {
        var people = await EmployeeNamesAsync(ct);
        return rows.Select(x => new WorkCommentDto(x.Id, x.AuthorEmployeeId,
            Name(people, x.AuthorEmployeeId) ?? "Administrator", x.Body, x.CreatedAt, x.UpdatedAt,
            CanManageOwn(x.AuthorEmployeeId), x.Version)).ToArray();
    }

    private async Task<IReadOnlyList<WorkLogDto>> MapWorklogsAsync(IReadOnlyList<WorkLog> rows, CancellationToken ct)
    {
        var people = await EmployeeNamesAsync(ct);
        return rows.Select(x => new WorkLogDto(x.Id, x.EmployeeId, Name(people, x.EmployeeId) ?? "Employee",
            x.WorkDate, x.Minutes, x.Description, x.CreatedAt,
            user.HasPermission(Permissions.WorkManage) || x.EmployeeId == user.EmployeeId, x.Version)).ToArray();
    }

    private async Task<IReadOnlyList<WorkHistoryDto>> MapHistoryAsync(IReadOnlyList<WorkItemHistory> rows, CancellationToken ct)
    {
        var people = await EmployeeNamesAsync(ct);
        return rows.Select(x => new WorkHistoryDto(x.Id, x.ActorEmployeeId,
            Name(people, x.ActorEmployeeId) ?? "Administrator", x.EventType, x.FieldName,
            x.BeforeValue, x.AfterValue, x.CreatedAt)).ToArray();
    }

    private async Task<Dictionary<Guid, string>> EmployeeNamesAsync(CancellationToken ct) =>
        (await employees.ListAsync(cancellationToken: ct)).ToDictionary(x => x.Id, x => x.FullName);

    private static string NormalizeProjectKey(string value)
    {
        Required(value, "Project key");
        var key = value.Trim().ToUpperInvariant();
        if (key.Length is < 2 or > 10 || !char.IsLetter(key[0]) || key.Any(c => !char.IsLetterOrDigit(c)))
            throw new DomainException("Project key must be 2 to 10 letters or numbers and start with a letter.");
        return key;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeLabels(IReadOnlyList<string>? labels) =>
        string.Join(',', (labels ?? []).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().Take(20));
    private static IReadOnlyList<Guid> NormalizeAssigneeIds(Guid? primaryAssigneeId, IReadOnlyList<Guid>? assigneeIds) =>
        (assigneeIds is not null ? assigneeIds : primaryAssigneeId.HasValue ? [primaryAssigneeId.Value] : [])
        .Where(x => x != Guid.Empty).Distinct().Take(20).ToArray();
    private static IReadOnlyList<string> SplitLabels(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string? Name(IReadOnlyDictionary<Guid, string> people, Guid? id) =>
        id.HasValue && people.TryGetValue(id.Value, out var name) ? name : null;
    private static void ValidateEstimate(int? minutes, decimal? points)
    {
        NonNegative(minutes, "Original estimate");
        if (points < 0) throw new DomainException("Story points cannot be negative.");
    }
    private static int? NonNegative(int? value, string name)
    {
        if (value < 0) throw new DomainException($"{name} cannot be negative.");
        return value;
    }
    private static void ValidateWorklog(DateOnly date, int minutes)
    {
        if (minutes is < 1 or > 1440) throw new DomainException("Worklog time must be between 1 and 1,440 minutes.");
        if (date > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            throw new DomainException("Work cannot be logged more than one day in the future.");
    }

    private static void ValidateItemEnums(WorkItemType type, WorkItemPriority priority)
    {
        if (!Enum.IsDefined(type) || !Enum.IsDefined(priority))
            throw new DomainException("Ticket type or priority is invalid.");
    }

    private async Task ValidateDailyWorklogTotalAsync(Guid employeeId, DateOnly date, int minutes, Guid? excludeId, CancellationToken ct)
    {
        var existing = await worklogs.ListAsync(x => x.EmployeeId == employeeId && x.WorkDate == date
            && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken: ct);
        if (existing.Sum(x => (long)x.Minutes) + minutes > 1440)
            throw new DomainException("Total logged work across all tasks cannot exceed 24 hours in one day.");
    }

    private enum AccessKind { Read, Create, Assign, Transition, Comment, Log }
}
