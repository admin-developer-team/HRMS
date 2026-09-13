using Hrms.Application;
using Hrms.Domain.Common;
using Hrms.Domain;
using Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Api.Middleware;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next(context);
    }
}

public sealed class TenantResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant, HrmsDbContext db)
    {
        if (context.Request.Path == "/health") { await next(context); return; }
        var baseDomain = configuration["Tenancy:BaseDomain"];
        var host = context.Request.Host.Host.TrimEnd('.');
        var hostSlug = TenantDomains.SlugForHost(host, baseDomain);
        if (hostSlug is null && context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            hostSlug = TenantDomains.SlugForHost(host, "localhost");
        if (hostSlug is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        if (hostSlug == "platform" && !string.Equals(host, baseDomain?.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        var hostTenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(
            x => x.Slug == hostSlug && !x.IsDeleted, context.RequestAborted);
        if (hostTenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        Guid? claimTenant = Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var claimId) ? claimId : null;
        Guid? headerTenant = Guid.TryParse(context.Request.Headers["X-Tenant-ID"].FirstOrDefault(), out var headerId) ? headerId : null;
        if ((claimTenant.HasValue && claimTenant != hostTenant.Id) || (headerTenant.HasValue && headerTenant != hostTenant.Id))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 403, Title = "Tenant mismatch", Detail = "The account does not belong to this company workspace." });
            return;
        }
        currentTenant.Set(hostTenant.Id, hostTenant.Slug);
        await next(context);
    }
}

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var (status, title) = exception switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                DomainException => (StatusCodes.Status400BadRequest, "Business rule violation"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
            };
            if (status >= 500) logger.LogError(exception, "Unhandled request error. CorrelationId={CorrelationId}", context.TraceIdentifier);
            else logger.LogWarning(exception, "Request rejected. CorrelationId={CorrelationId}", context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status, Title = title,
                Detail = status == 500 ? "An unexpected error occurred. Use the correlation ID when contacting support." : exception.Message,
                Extensions = { ["correlationId"] = context.TraceIdentifier }
            });
        }
    }
}
