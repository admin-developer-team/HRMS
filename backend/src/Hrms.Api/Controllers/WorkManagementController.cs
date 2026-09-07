using Hrms.Application;
using Hrms.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/work"), Authorize(Policy = Permissions.WorkRead)]
public sealed class WorkManagementController(IWorkManagementService service) : ControllerBase
{
    [HttpGet("overview")]
    public Task<WorkOverviewDto> Overview(CancellationToken ct) => service.GetOverviewAsync(ct);

    [HttpGet("projects")]
    public Task<IReadOnlyList<WorkProjectDto>> Projects(CancellationToken ct) => service.ListProjectsAsync(ct);

    [HttpPost("projects"), Authorize(Policy = Permissions.WorkManage)]
    public Task<WorkProjectDto> CreateProject(CreateWorkProjectRequest request, CancellationToken ct) =>
        service.CreateProjectAsync(request, ct);

    [HttpPut("projects/{id:guid}"), Authorize(Policy = Permissions.WorkManage)]
    public Task<WorkProjectDto> UpdateProject(Guid id, UpdateWorkProjectRequest request, CancellationToken ct) =>
        service.UpdateProjectAsync(id, request, ct);

    [HttpGet("projects/{projectId:guid}/members")]
    public Task<IReadOnlyList<WorkProjectMemberDto>> Members(Guid projectId, CancellationToken ct) =>
        service.ListMembersAsync(projectId, ct);

    [HttpPut("projects/{projectId:guid}/members"), Authorize(Policy = Permissions.WorkManage)]
    public Task<IReadOnlyList<WorkProjectMemberDto>> SetMembers(Guid projectId, IReadOnlyList<SetWorkProjectMemberRequest> request, CancellationToken ct) =>
        service.SetMembersAsync(projectId, request, ct);

    [HttpGet("items")]
    public Task<PagedResult<WorkItemDto>> Items(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] string? search = null,
        [FromQuery] Guid? projectId = null, [FromQuery] WorkItemStatus? status = null,
        [FromQuery] WorkItemPriority? priority = null, [FromQuery] Guid? assigneeEmployeeId = null,
        CancellationToken ct = default, [FromQuery] Guid? sprintId = null, [FromQuery] bool backlogOnly = false, [FromQuery] WorkItemType? type = null) =>
        service.SearchItemsAsync(new(page, pageSize, search), projectId, status, priority, assigneeEmployeeId, ct, sprintId, backlogOnly, type);

    [HttpGet("items/{id:guid}")]
    public Task<WorkItemDetailDto> Item(Guid id, CancellationToken ct) => service.GetItemAsync(id, ct);

    [HttpPost("items"), Authorize(Policy = Permissions.WorkCreate)]
    public Task<WorkItemDto> CreateItem(CreateWorkItemRequest request, CancellationToken ct) =>
        service.CreateItemAsync(request, ct);

    [HttpPut("items/{id:guid}"), Authorize(Policy = Permissions.WorkCreate)]
    public Task<WorkItemDto> UpdateItem(Guid id, UpdateWorkItemRequest request, CancellationToken ct) =>
        service.UpdateItemAsync(id, request, ct);

    [HttpPut("items/{id:guid}/assignee"), Authorize(Policy = Permissions.WorkAssign)]
    public Task<WorkItemDto> Assign(Guid id, AssignWorkItemRequest request, CancellationToken ct) =>
        service.AssignItemAsync(id, request, ct);

    [HttpPut("items/{id:guid}/transition"), Authorize(Policy = Permissions.WorkTransition)]
    public Task<WorkItemDto> Transition(Guid id, TransitionWorkItemRequest request, CancellationToken ct) =>
        service.TransitionItemAsync(id, request, ct);

    [HttpPost("items/{id:guid}/comments"), Authorize(Policy = Permissions.WorkComment)]
    public Task<WorkCommentDto> Comment(Guid id, CreateWorkCommentRequest request, CancellationToken ct) =>
        service.AddCommentAsync(id, request, ct);

    [HttpPut("items/{id:guid}/comments/{commentId:guid}"), Authorize(Policy = Permissions.WorkComment)]
    public Task<WorkCommentDto> UpdateComment(Guid id, Guid commentId, UpdateWorkCommentRequest request, CancellationToken ct) =>
        service.UpdateCommentAsync(id, commentId, request, ct);

    [HttpDelete("items/{id:guid}/comments/{commentId:guid}"), Authorize(Policy = Permissions.WorkComment)]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        await service.DeleteCommentAsync(id, commentId, ct);
        return NoContent();
    }

    [HttpPost("items/{id:guid}/worklogs"), Authorize(Policy = Permissions.WorkLog)]
    public Task<WorkLogDto> LogWork(Guid id, CreateWorkLogRequest request, CancellationToken ct) =>
        service.AddWorklogAsync(id, request, ct);

    [HttpPut("items/{id:guid}/worklogs/{worklogId:guid}"), Authorize(Policy = Permissions.WorkLog)]
    public Task<WorkLogDto> UpdateWorklog(Guid id, Guid worklogId, UpdateWorkLogRequest request, CancellationToken ct) =>
        service.UpdateWorklogAsync(id, worklogId, request, ct);

    [HttpDelete("items/{id:guid}/worklogs/{worklogId:guid}"), Authorize(Policy = Permissions.WorkLog)]
    public async Task<IActionResult> DeleteWorklog(Guid id, Guid worklogId, CancellationToken ct)
    {
        await service.DeleteWorklogAsync(id, worklogId, ct);
        return NoContent();
    }

    [HttpGet("reports/time")]
    public Task<WorkTimeReportDto> TimeReport(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? projectId = null,
        [FromQuery] Guid? employeeId = null, CancellationToken ct = default) =>
        service.GetTimeReportAsync(from, to, projectId, employeeId, ct);
}
