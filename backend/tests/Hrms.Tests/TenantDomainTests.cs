using Hrms.Application;

namespace Hrms.Tests;

public sealed class TenantDomainTests
{
    [Theory]
    [InlineData("hrms.ssym.co.in", "platform")]
    [InlineData("acme.hrms.ssym.co.in", "acme")]
    [InlineData("Acme.HRMS.SSYM.CO.IN", "acme")]
    [InlineData("acme.hrms.ssym.co.in.", "acme")]
    [InlineData("acme.other.example", null)]
    [InlineData("acme.hrms.ssym.co.in.evil.test", null)]
    [InlineData("too.deep.hrms.ssym.co.in", null)]
    [InlineData("-bad.hrms.ssym.co.in", null)]
    public void Host_resolves_only_one_valid_workspace_label(string host, string? expected)
    {
        Assert.Equal(expected, TenantDomains.SlugForHost(host, "hrms.ssym.co.in"));
    }

    [Fact]
    public void Email_urls_use_the_tenant_host()
    {
        Assert.Equal("https://acme.hrms.ssym.co.in",
            TenantDomains.BaseUrlForTenant("https://hrms.ssym.co.in", "hrms.ssym.co.in", "acme"));
        Assert.Equal("https://hrms.ssym.co.in",
            TenantDomains.BaseUrlForTenant("https://hrms.ssym.co.in", "hrms.ssym.co.in", "platform"));
    }

    [Fact]
    public void Local_email_urls_keep_the_frontend_port_and_workspace_subdomain()
    {
        Assert.Equal("http://acme.localhost:4200", TenantDomains.BaseUrlForTenant("http://localhost:4200", "hrms.ssym.co.in", "acme"));
        Assert.Equal("http://localhost:4200", TenantDomains.BaseUrlForTenant("http://localhost:4200", "hrms.ssym.co.in", "platform"));
    }
}
