using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hrms.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hrms.Infrastructure.Identity;

public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public string? Slug { get; private set; }
    public void Set(Guid tenantId, string? slug = null) { TenantId = tenantId; Slug = slug; }
    public void Clear() { TenantId = null; Slug = null; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid? EmployeeId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("employee_id"), out var id) ? id : null;
    public bool IsPlatformAdmin => string.Equals(accessor.HttpContext?.User.FindFirstValue("platform_admin"), "true", StringComparison.OrdinalIgnoreCase);
    public bool HasPermission(string permission) => accessor.HttpContext?.User.HasClaim("permission", Permissions.All) == true || accessor.HttpContext?.User.HasClaim("permission", permission) == true;
}

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, 32);
        return $"pbkdf2-sha512${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]); var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "Hrms.Api";
    public string Audience { get; set; } = "Hrms.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    public int RefreshTokenLifetimeDays => _options.RefreshTokenDays;
    public int AccessTokenLifetimeMinutes => _options.AccessTokenMinutes;
    public string CreateAccessToken(Guid userId, Guid tenantId, Guid? employeeId, string email, bool isPlatformAdmin, IEnumerable<string> roles, IEnumerable<string> permissions, Guid? sessionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Email, email),
            new("tenant_id", tenantId.ToString()), new("platform_admin", isPlatformAdmin.ToString().ToLowerInvariant())
        };
        if (employeeId.HasValue) claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        if (sessionId.HasValue) claims.Add(new Claim("session_id", sessionId.Value.ToString()));
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(permissions.Select(x => new Claim("permission", x)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(_options.Issuer, _options.Audience, claims, expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes), signingCredentials: credentials));
    }
    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
