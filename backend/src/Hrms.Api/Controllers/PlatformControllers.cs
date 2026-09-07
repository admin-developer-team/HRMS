using Hrms.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous] public Task<TokenResponse> Login(LoginRequest request, CancellationToken ct) => service.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
    [HttpPost("refresh"), AllowAnonymous] public Task<TokenResponse> Refresh(RefreshRequest request, CancellationToken ct) => service.RefreshAsync(request, ct);
    [HttpPost("revoke"), Authorize] public async Task<IActionResult> Revoke(RefreshRequest request, CancellationToken ct) { await service.RevokeAsync(request, ct); return NoContent(); }
}

[ApiController, Route("api/v1/platform/tenants"), Authorize(Policy = "PlatformAdmin")]
public sealed class TenantsController(ITenantService service) : ControllerBase
{
    [HttpPost] public async Task<ActionResult<TenantDto>> Create(CreateTenantRequest request, CancellationToken ct) { var result = await service.CreateAsync(request, ct); return CreatedAtAction(nameof(Search), new { search = result.Slug }, result); }
    [HttpGet] public Task<PagedResult<TenantDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize, search), ct);
}

[ApiController, Route("api/v1/identity"), Authorize(Policy = Permissions.IdentityManage)]
public sealed class IdentityController(IIdentityAdminService service) : ControllerBase
{
    [HttpPost("roles")] public Task<RoleDto> CreateRole(CreateRoleRequest request, CancellationToken ct) => service.CreateRoleAsync(request, ct);
    [HttpGet("roles")] public Task<IReadOnlyList<RoleDto>> Roles(CancellationToken ct) => service.ListRolesAsync(ct);
    [HttpPost("users")] public Task<UserAdminDto> CreateUser(CreateUserRequest request, CancellationToken ct) => service.CreateUserAsync(request, ct);
    [HttpPost("employees/{employeeId:guid}/account")] public Task<UserAdminDto> ProvisionEmployee(Guid employeeId, ProvisionEmployeeAccountRequest request, CancellationToken ct) => service.ProvisionEmployeeAsync(employeeId, request, ct);
    [HttpPut("users/{userId:guid}/roles")] public Task<UserAdminDto> SetRoles(Guid userId, SetUserRolesRequest request, CancellationToken ct) => service.SetRolesAsync(userId, request, ct);
    [HttpPut("users/{userId:guid}/password")] public Task<UserAdminDto> ResetPassword(Guid userId, ResetUserPasswordRequest request, CancellationToken ct) => service.ResetPasswordAsync(userId, request, ct);
    [HttpGet("users")] public Task<PagedResult<UserAdminDto>> Users([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, CancellationToken ct = default) => service.SearchUsersAsync(new(page, pageSize, search), ct);
}

[ApiController, Route("api/v1/dashboard"), Authorize(Policy = Permissions.DashboardAdmin)]
public sealed class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet] public Task<DashboardDto> Get(CancellationToken ct) => service.GetAsync(ct);
}

[ApiController, Route("api/v1/audit-logs"), Authorize(Policy = Permissions.AuditRead)]
public sealed class AuditLogsController(IAuditReader service) : ControllerBase
{
    [HttpGet] public Task<PagedResult<AuditLogDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? entityType = null, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize), entityType, ct);
}
