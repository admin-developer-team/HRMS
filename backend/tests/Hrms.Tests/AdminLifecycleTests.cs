using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class AdminLifecycleTests
{
    [Fact]
    public async Task Administrator_can_change_employee_identity_and_deactivation_revokes_access()
    {
        using var h = new Harness();
        var user = new UserAccount { TenantId = h.TenantId, Email = "old@example.test", DisplayName = "Old Name", IsActive = true };
        var employee = new Employee { TenantId = h.TenantId, UserId = user.Id, EmployeeNumber = "OLD-1", FirstName = "Old", LastName = "Name", WorkEmail = user.Email, HireDate = new(2025, 1, 1), BaseSalary = 500000, SalaryCurrency = "INR" };
        var token = new RefreshToken { TenantId = h.TenantId, UserId = user.Id, TokenHash = "test-token", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        h.Db.Users.Add(user); h.Db.Employees.Add(employee); h.Db.RefreshTokens.Add(token); await h.Db.SaveChangesAsync();

        var service = h.EmployeeService();
        var updated = await service.UpdateAsync(employee.Id, new(
            "NEW-7", "New", "Name", "new@example.test", "+91 9000000000", new(2024, 6, 10), EmploymentStatus.Inactive,
            EmploymentType.Permanent, null, null, null, null, 650000, "INR", employee.Version), default);

        Assert.Equal("NEW-7", updated.EmployeeNumber);
        Assert.Equal("New", updated.FirstName);
        Assert.Equal(new DateOnly(2024, 6, 10), updated.HireDate);
        Assert.False(user.IsActive);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal("new@example.test", user.Email);
    }

    [Fact]
    public async Task Employee_number_remains_unique_when_an_administrator_edits_it()
    {
        using var h = new Harness();
        h.Db.Employees.Add(new Employee { TenantId = h.TenantId, EmployeeNumber = "TAKEN", FirstName = "First", WorkEmail = "first@example.test", HireDate = new(2025, 1, 1) });
        var target = new Employee { TenantId = h.TenantId, EmployeeNumber = "TARGET", FirstName = "Second", WorkEmail = "second@example.test", HireDate = new(2025, 1, 1) };
        h.Db.Employees.Add(target); await h.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => h.EmployeeService().UpdateAsync(target.Id, new(
            "TAKEN", "Second", "Employee", target.WorkEmail, null, target.HireDate, EmploymentStatus.Active,
            EmploymentType.Permanent, null, null, null, null, 0, "INR", target.Version), default));
    }

    [Fact]
    public async Task Platform_administrator_can_convert_trial_and_manage_subscription()
    {
        using var h = new Harness();
        var customer = new Tenant { Name = "Trial Company", Slug = "trial-company", Status = TenantStatus.Trial, TrialEndsAt = DateTimeOffset.UtcNow.AddDays(10), DefaultCurrency = "USD", TimeZone = "UTC" };
        var subscription = new TenantSubscription { TenantId = customer.Id, PlanCode = "trial", EmployeeLimit = 10, StartsAt = DateTimeOffset.UtcNow.AddDays(-20), EndsAt = DateTimeOffset.UtcNow.AddDays(10), IsActive = true };
        h.Db.Tenants.Add(customer); h.Db.TenantSubscriptions.Add(subscription); await h.Db.SaveChangesAsync();

        var service = new TenantService(h.R<Tenant>(), h.R<TenantSubscription>(), h.R<UserAccount>(), h.R<Role>(), h.R<UserRole>(), h.R<LeaveType>(), h.R<Employee>(), h.R<RefreshToken>(), new Pbkdf2PasswordHasher(), h.Db, h.Tenant);
        var updated = await service.UpdateAsync(customer.Id, new(
            "Permanent Company", TenantStatus.Active, "INR", "Asia/Kolkata", null, "enterprise", 100,
            DateTimeOffset.UtcNow.Date, DateTimeOffset.UtcNow.AddYears(1), true, customer.Version, subscription.Version), default);

        Assert.Equal(TenantStatus.Active, updated.Status);
        Assert.Null(updated.TrialEndsAt);
        Assert.Equal("enterprise", updated.PlanCode);
        Assert.Equal(100, updated.EmployeeLimit);
        Assert.True(updated.SubscriptionActive);
    }

    [Fact]
    public async Task Platform_access_dates_and_switch_override_a_paid_provider_period()
    {
        using var h = new Harness();
        var now = DateTimeOffset.UtcNow;
        var customer = new Tenant { Name = "Paid Company", Slug = "paid-company", Status = TenantStatus.Active, DefaultCurrency = "INR", TimeZone = "UTC" };
        var paid = new TenantSubscription { TenantId = customer.Id, PlanCode = "starter", EmployeeLimit = 50,
            StartsAt = now.AddDays(-1), EndsAt = now.AddMonths(1), IsActive = true, BillingProvider = "razorpay_test" };
        h.Db.Tenants.Add(customer); h.Db.TenantSubscriptions.Add(paid); await h.Db.SaveChangesAsync();
        var service = new TenantService(h.R<Tenant>(), h.R<TenantSubscription>(), h.R<UserAccount>(), h.R<Role>(), h.R<UserRole>(), h.R<LeaveType>(), h.R<Employee>(), h.R<RefreshToken>(), new Pbkdf2PasswordHasher(), h.Db, h.Tenant);

        var shortened = await service.UpdateAsync(customer.Id, new("Paid Company", TenantStatus.Active, "INR", "UTC", null,
            "starter", 50, paid.StartsAt, now.AddMinutes(-1), true, customer.Version, paid.Version), default);
        Assert.Equal(now.AddMinutes(-1), shortened.SubscriptionEndsAt);
        Assert.False(customer.AdminAccessAt(now));

        paid.EndsAt = now.AddMonths(2); // A subsequent payment update must not restore admin access.
        await h.Db.SaveChangesAsync();
        Assert.False(customer.AdminAccessAt(now));

        var extended = await service.UpdateAsync(customer.Id, new("Paid Company", TenantStatus.Active, "INR", "UTC", null,
            "starter", 50, paid.StartsAt, now.AddMonths(3), true, customer.Version, paid.Version), default);
        Assert.True(customer.AdminAccessAt(now));
        Assert.Equal(now.AddMonths(3), extended.SubscriptionEndsAt);

        await service.UpdateAsync(customer.Id, new("Paid Company", TenantStatus.Active, "INR", "UTC", null,
            "starter", 50, paid.StartsAt, now.AddMonths(3), false, customer.Version, paid.Version), default);
        Assert.False(customer.AdminAccessAt(now));
    }

    private sealed class Actor : ICurrentUser
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public Guid? EmployeeId { get; set; } = Guid.NewGuid();
        public bool IsPlatformAdmin => true;
        public bool HasPermission(string permission) => true;
    }

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public CurrentTenant Tenant { get; } = new();
        public Actor User { get; } = new();
        public HrmsDbContext Db { get; }

        public Harness()
        {
            Tenant.Set(TenantId);
            Db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Tenant, User, new TestNotificationPublisher());
            Db.Tenants.Add(new Tenant { Id = TenantId, Name = "Admin test", Slug = "admin-test", Status = TenantStatus.Active, DefaultCurrency = "INR", TimeZone = "UTC" });
            Db.TenantSubscriptions.Add(new TenantSubscription { TenantId = TenantId, PlanCode = "enterprise", EmployeeLimit = 100, StartsAt = DateTimeOffset.UtcNow.AddDays(-1), IsActive = true });
            Db.SaveChanges();
        }

        public EmployeeService EmployeeService() => new(R<Employee>(), R<UserAccount>(), R<RefreshToken>(), R<TenantSubscription>(), R<AuditLog>(), Tenant, User, Db, R<Department>(), R<Designation>(), R<Location>());
        public Repository<T> R<T>() where T : AuditableEntity => new(Db);
        public void Dispose() => Db.Dispose();
    }
}
