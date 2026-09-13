using Hrms.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/search"), Authorize]
public sealed class GlobalSearchController(IGlobalSearchService service) : ControllerBase
{
    [HttpGet]
    public Task<GlobalSearchResponse> Search([FromQuery] string q = "", CancellationToken ct = default) => service.SearchAsync(q, ct);
}
