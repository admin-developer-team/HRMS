using Hrms.Application;

namespace Hrms.Tests;

public sealed class TenantDomainTests
{
    [Theory]
    [InlineData("hrms.avntechnologies.co.in", "platform")]
    [InlineData("acme.hrms.avntechnologies.co.in", "acme")]
    [InlineData("Acme.HRMS.AVNTECHNOLOGIES.CO.IN", "acme")]
    [InlineData("acme.hrms.avntechnologies.co.in.", "acme")]
    [InlineData("acme.other.example", null)]
    [InlineData("acme.hrms.avntechnologies.co.in.evil.test", null)]
    [InlineData("too.deep.hrms.avntechnologies.co.in", null)]
    [InlineData("-bad.hrms.avntechnologies.co.in", null)]
    public void Host_resolves_only_one_valid_workspace_label(string host, string? expected)
    {
        Assert.Equal(expected, TenantDomains.SlugForHost(host, "hrms.avntechnologies.co.in"));
    }

    [Fact]
    public void Email_urls_use_the_tenant_host()
    {
        Assert.Equal("https://acme.hrms.avntechnologies.co.in",
            TenantDomains.BaseUrlForTenant("https://hrms.avntechnologies.co.in", "hrms.avntechnologies.co.in", "acme"));
        Assert.Equal("https://hrms.avntechnologies.co.in",
            TenantDomains.BaseUrlForTenant("https://hrms.avntechnologies.co.in", "hrms.avntechnologies.co.in", "platform"));
    }
}
