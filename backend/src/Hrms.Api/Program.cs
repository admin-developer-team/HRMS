using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hrms.Api;
using Hrms.Api.Middleware;
using Hrms.Application;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Persistence;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddDebug();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HRMS API",
        Version = "v1",
        Description = "Multi-tenant enterprise HRMS backend API"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT access token returned by /api/v1/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.IsAuthenticated == true
                ? $"{context.User.FindFirst("tenant_id")?.Value}:{context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}"
                : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationMiddleware>();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false);
if (swaggerEnabled)
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HRMS API v1");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();
    });
}
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

if (args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider.GetRequiredService<DemoCompanySeeder>().SeedAsync();
    Console.WriteLine(result.Created
        ? $"Created demo tenant '{result.TenantSlug}' with {result.Accounts.Count} accounts."
        : $"Demo tenant '{result.TenantSlug}' already exists; no data was changed.");
    return;
}

await app.RunAsync();

public partial class Program;
