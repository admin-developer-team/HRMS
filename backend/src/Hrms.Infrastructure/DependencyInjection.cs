using System.Security.Claims;
using System.Text;
using Hrms.Application;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Documents;
using Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Hrms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var configuredConnection = configuration.GetConnectionString("Hrms");
        if (string.IsNullOrWhiteSpace(configuredConnection)) throw new InvalidOperationException("ConnectionStrings:Hrms is required.");
        var connectionString = PostgresConnectionString.Normalize(configuredConnection);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddDbContext<HrmsDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(HrmsDbContext).Assembly.FullName)));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<HrmsDbContext>());
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<SessionValidator>();
        services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = ClaimTypes.Email, RoleClaimType = ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (context.Principal is null || !await context.HttpContext.RequestServices.GetRequiredService<SessionValidator>()
                        .ValidateAsync(context.Principal, context.HttpContext.RequestAborted)) context.Fail("This session is no longer valid.");
                },
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken)
                        && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications", StringComparison.Ordinal))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorizationBuilder()
            .AddPolicy("PlatformAdmin", policy => policy.RequireClaim("platform_admin", "true"))
            .AddPolicy("EmployeeLinked", policy => policy.RequireClaim("employee_id"));
        foreach (var permission in Permissions.Catalog)
            services.AddAuthorizationBuilder().AddPolicy(permission, policy => policy.RequireAssertion(ctx => ctx.User.HasClaim("permission", Permissions.All) || ctx.User.HasClaim("permission", permission)));

        services.AddScoped<ITenantService, TenantService>(); services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityAdminService, IdentityAdminService>();
        services.AddScoped<IEmployeeService, EmployeeService>(); services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ILeaveService, LeaveService>(); services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IWorkforceOperationsService, WorkforceOperationsService>();
        services.AddScoped<IPayrollService, PayrollService>(); services.AddScoped<IRecruitmentService, RecruitmentService>();
        services.AddScoped<IPerformanceService, PerformanceService>(); services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IExpenseService, ExpenseService>(); services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<IDashboardService, DashboardService>(); services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<ISelfService, SelfService>();
        services.AddScoped<AttendanceCorrectionService>();
        services.AddScoped<WorkPlanningService>();
        services.AddScoped<IWorkManagementService, WorkManagementService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ICompanyProfileService, CompanyProfileService>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<DemoCompanySeeder>();
        return services;
    }
}
