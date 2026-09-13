namespace Hrms.Application;

public static class TenantDomains
{
    public static string? SlugForHost(string host, string? baseDomain)
    {
        if (string.IsNullOrWhiteSpace(baseDomain)) return null;
        var domain = baseDomain.Trim().TrimEnd('.').ToLowerInvariant();
        var name = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (name == domain) return "platform";
        var suffix = "." + domain;
        if (!name.EndsWith(suffix, StringComparison.Ordinal)) return null;
        var slug = name[..^suffix.Length];
        return IsValidSlug(slug) ? slug : null;
    }

    public static bool IsValidSlug(string slug) => slug.Length is >= 3 and <= 63
        && char.IsAsciiLetterOrDigit(slug[0]) && char.IsAsciiLetterOrDigit(slug[^1])
        && slug.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public static string BaseUrlForTenant(string? configuredUrl, string? baseDomain, string slug)
    {
        if (string.IsNullOrWhiteSpace(baseDomain)) return configuredUrl?.TrimEnd('/') ?? string.Empty;
        var domain = baseDomain.Trim().TrimEnd('.').ToLowerInvariant();
        return $"https://{(slug == "platform" ? domain : slug + "." + domain)}";
    }
}
