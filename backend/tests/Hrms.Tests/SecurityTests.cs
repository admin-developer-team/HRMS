using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure;
using Hrms.Application;
using Microsoft.Extensions.Options;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace Hrms.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Pbkdf2_hash_round_trips_and_rejects_wrong_password()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("A-long-and-safe-test-password!1");
        Assert.True(hasher.Verify("A-long-and-safe-test-password!1", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
        Assert.DoesNotContain("A-long-and-safe", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Employee_token_contains_employee_identity_and_only_assigned_permissions()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            SigningKey = "test-signing-key-that-is-longer-than-thirty-two-bytes-1234567890",
            Issuer = "test", Audience = "test-client"
        }));
        var employeeId = Guid.NewGuid();
        var token = service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), employeeId, "employee@example.test", false, ["EMPLOYEE_SELF_SERVICE"], [Permissions.SelfService]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(employeeId.ToString(), jwt.Claims.Single(x => x.Type == "employee_id").Value);
        Assert.Contains(jwt.Claims, x => x.Type == "permission" && x.Value == Permissions.SelfService);
        Assert.DoesNotContain(jwt.Claims, x => x.Type == "permission" && x.Value == Permissions.EmployeesManage);
    }

    [Fact]
    public void Professional_system_roles_use_granular_permissions()
    {
        var employee = Permissions.TenantSystemRoles.Single(x => x.NormalizedName == "EMPLOYEE_SELF_SERVICE");
        Assert.Equal([Permissions.SelfService], employee.Permissions);
        Assert.DoesNotContain(Permissions.All, employee.Permissions);
    }

    [Fact]
    public void PostgreSql_url_is_normalized_for_npgsql_and_preserves_tls_settings()
    {
        var normalized = PostgresConnectionString.Normalize("postgresql://hrms_user:p%40ssword@db.example.test:5433/hrms_prod?sslmode=require&channel_binding=require");
        var connection = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("db.example.test", connection.Host);
        Assert.Equal(5433, connection.Port);
        Assert.Equal("hrms_user", connection.Username);
        Assert.Equal("p@ssword", connection.Password);
        Assert.Equal("hrms_prod", connection.Database);
        Assert.Equal(SslMode.Require, connection.SslMode);
        Assert.Equal("Require", connection["Channel Binding"]?.ToString());
    }
}
