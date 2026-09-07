using Hrms.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/work"), Authorize(Policy = Permissions.WorkRead)]
public sealed class WorkPlanningController(WorkPlanningService service) : ControllerBase
{
    [HttpGet("projects/{id:guid}/sprints")]
    public Task<IReadOnlyList<SprintDto>> Sprints(Guid id, CancellationToken ct) => service.ListAsync(id, ct);
    [HttpPost("projects/{id:guid}/sprints")]
    public Task<SprintDto> Create(Guid id, CreateSprintRequest request, CancellationToken ct) => service.CreateAsync(id, request, ct);
    [HttpPut("sprints/{id:guid}/status")]
    public Task<SprintDto> Change(Guid id, ChangeSprintRequest request, CancellationToken ct) => service.ChangeAsync(id, request, ct);
    [HttpPut("items/{id:guid}/sprint")]
    public Task<WorkItemDto> Plan(Guid id, PlanWorkItemRequest request, CancellationToken ct) => service.PlanAsync(id, request, ct);
    [HttpGet("items/{id:guid}/children")]
    public Task<IReadOnlyList<WorkItemDto>> Children(Guid id, CancellationToken ct) => service.ChildrenAsync(id, ct);
}
