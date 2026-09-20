using System.Security.Claims;
using Hrms.Api.Middleware;
using Hrms.Domain;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Hrms.Tests;

public sealed class TenantHostMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/billing/webhooks/razorpay")]
    [InlineData("/api/v1/billing/webhooks/cashfree")]
    public async Task Signed_billing_webhooks_accept_public_tunnel_host_without_resolving_tenant(string path)
    {
        var tenant = new CurrentTenant();
        await using var db = CreateDb(tenant);
        var context = Context("example-tunnel.example", Guid.NewGuid());
        context.Request.Path = path;
        var called = false;
        var middleware = Middleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tenant, db);

        Assert.True(called);
        Assert.Null(tenant.TenantId);
    }

    [Fact]
    public async Task Tenant_host_rejects_another_tenants_access_token()
    {
        var first = new Tenant { Slug = "first", Name = "First" };
        var second = new Tenant { Slug = "second", Name = "Second" };
        var tenant = new CurrentTenant();
        await using var db = CreateDb(tenant);
        db.Tenants.AddRange(first, second);
        await db.SaveChangesAsync();
        var context = Context("first.hrms.ssym.co.in", second.Id);
        var called = false;
        var middleware = Middleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tenant, db);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task Matching_host_and_token_resolve_same_tenant()
    {
        var company = new Tenant { Slug = "acme", Name = "Acme" };
        var tenant = new CurrentTenant();
        await using var db = CreateDb(tenant);
        db.Tenants.Add(company);
        await db.SaveChangesAsync();
        var context = Context("acme.hrms.ssym.co.in", company.Id);
        var called = false;
        var middleware = Middleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tenant, db);

        Assert.True(called);
        Assert.Equal(company.Id, tenant.TenantId);
        Assert.Equal("acme", tenant.Slug);
    }

    private static TenantResolutionMiddleware Middleware(RequestDelegate next) => new(next,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Tenancy:BaseDomain"] = "hrms.ssym.co.in" }).Build());

    private static DefaultHttpContext Context(string host, Guid claimTenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", claimTenant.ToString())], "test"));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static HrmsDbContext CreateDb(CurrentTenant tenant) => new(
        new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        tenant, new TestCurrentUser(), new TestNotificationPublisher());
}
