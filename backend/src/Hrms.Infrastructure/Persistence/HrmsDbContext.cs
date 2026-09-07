using System.Linq.Expressions;
using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hrms.Infrastructure.Persistence;

public sealed class HrmsDbContext(DbContextOptions<HrmsDbContext> options, ICurrentTenant currentTenant, ICurrentUser currentUser,
    INotificationPublisher notificationPublisher)
    : DbContext(options), IUnitOfWork
{
    public Guid? CurrentTenantId => currentTenant.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<StoredDocument> StoredDocuments => Set<StoredDocument>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AttendancePolicy> AttendancePolicies => Set<AttendancePolicy>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceCorrection> AttendanceCorrections => Set<AttendanceCorrection>();
    public DbSet<WorkSprint> WorkSprints => Set<WorkSprint>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();
    public DbSet<JobOpening> JobOpenings => Set<JobOpening>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<PerformanceCycle> PerformanceCycles => Set<PerformanceCycle>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<TrainingCourse> TrainingCourses => Set<TrainingCourse>();
    public DbSet<TrainingEnrollment> TrainingEnrollments => Set<TrainingEnrollment>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<WorkProject> WorkProjects => Set<WorkProject>();
    public DbSet<WorkProjectMember> WorkProjectMembers => Set<WorkProjectMember>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemAssignee> WorkItemAssignees => Set<WorkItemAssignee>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<WorkItemHistory> WorkItemHistories => Set<WorkItemHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(x => typeof(AuditableEntity).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).HasKey(nameof(Entity.Id));
            modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.Version)).IsConcurrencyToken();
            ApplyGlobalFilter(modelBuilder, entityType.ClrType);
        }

        modelBuilder.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<UserAccount>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
        modelBuilder.Entity<UserRole>().HasIndex(x => new { x.TenantId, x.UserId, x.RoleId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(x => new { x.TenantId, x.EmployeeNumber }).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(x => new { x.TenantId, x.WorkEmail }).IsUnique();
        modelBuilder.Entity<Department>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<Designation>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<Location>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<AttendanceRecord>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate });
        modelBuilder.Entity<AttendanceRecord>().HasIndex(x => new { x.TenantId, x.EmployeeId })
            .IsUnique().HasFilter("\"IsDeleted\" = false AND \"ClockedOutAt\" IS NULL");
        modelBuilder.Entity<AttendanceCorrection>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate, x.Status });
        modelBuilder.Entity<WorkSprint>().HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        modelBuilder.Entity<WorkSprint>().HasIndex(x => new { x.TenantId, x.ProjectId })
            .IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" = 1");
        modelBuilder.Entity<WorkItem>().HasIndex(x => new { x.TenantId, x.ProjectId, x.SprintId, x.Status });
        modelBuilder.Entity<AttendancePolicy>().HasIndex(x => x.TenantId).IsUnique();
        modelBuilder.Entity<LeaveType>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<LeaveBalance>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
        modelBuilder.Entity<PayrollRun>().HasIndex(x => new { x.TenantId, x.PeriodStart, x.PeriodEnd });
        modelBuilder.Entity<PayrollItem>().HasIndex(x => new { x.TenantId, x.PayrollRunId, x.EmployeeId }).IsUnique();
        modelBuilder.Entity<JobOpening>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<Candidate>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        modelBuilder.Entity<JobApplication>().HasIndex(x => new { x.TenantId, x.JobOpeningId, x.CandidateId }).IsUnique();
        modelBuilder.Entity<PerformanceReview>().HasIndex(x => new { x.TenantId, x.CycleId, x.EmployeeId, x.ReviewerId }).IsUnique();
        modelBuilder.Entity<Asset>().HasIndex(x => new { x.TenantId, x.AssetTag }).IsUnique();
        modelBuilder.Entity<ExpenseClaim>().HasIndex(x => new { x.TenantId, x.ClaimNumber }).IsUnique();
        modelBuilder.Entity<TrainingEnrollment>().HasIndex(x => new { x.TenantId, x.CourseId, x.EmployeeId });
        modelBuilder.Entity<WorkProject>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        modelBuilder.Entity<WorkProjectMember>().HasIndex(x => new { x.TenantId, x.ProjectId, x.EmployeeId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        modelBuilder.Entity<WorkItem>().HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
        modelBuilder.Entity<WorkItem>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        modelBuilder.Entity<WorkItem>().HasIndex(x => new { x.TenantId, x.Status, x.AssigneeEmployeeId });
        modelBuilder.Entity<WorkItemAssignee>().HasIndex(x => new { x.TenantId, x.WorkItemId, x.EmployeeId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        modelBuilder.Entity<WorkItemAssignee>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkItemId });
        modelBuilder.Entity<WorkItemComment>().HasIndex(x => new { x.TenantId, x.WorkItemId, x.CreatedAt });
        modelBuilder.Entity<WorkLog>().HasIndex(x => new { x.TenantId, x.WorkItemId, x.WorkDate });
        modelBuilder.Entity<WorkLog>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate });
        modelBuilder.Entity<WorkItemHistory>().HasIndex(x => new { x.TenantId, x.WorkItemId, x.CreatedAt });
        modelBuilder.Entity<StoredDocument>().HasIndex(x => new { x.TenantId, x.OwnerType, x.OwnerId, x.Category, x.CreatedAt });
        modelBuilder.Entity<UserNotification>().HasIndex(x => new { x.TenantId, x.UserId, x.ReadAt, x.CreatedAt });

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18); property.SetScale(2);
        }
        modelBuilder.Entity<AttendanceRecord>().Property(x => x.ClockInLatitude).HasPrecision(9, 6);
        modelBuilder.Entity<AttendanceRecord>().Property(x => x.ClockInLongitude).HasPrecision(9, 6);
        modelBuilder.Entity<AttendanceRecord>().Property(x => x.ClockOutLatitude).HasPrecision(9, 6);
        modelBuilder.Entity<AttendanceRecord>().Property(x => x.ClockOutLongitude).HasPrecision(9, 6);
        modelBuilder.Entity<AttendanceRecord>().Property(x => x.ClockInAccuracyMeters).HasPrecision(10, 2);
        modelBuilder.Entity<AttendanceRecord>().Property(x => x.ClockOutAccuracyMeters).HasPrecision(10, 2);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType.IsEnum))
            property.SetMaxLength(40);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var notificationUserIds = ChangeTracker.Entries<UserNotification>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => x.Entity.UserId).Distinct().ToArray();
        var changed = ChangeTracker.Entries<AuditableEntity>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(x => x.Entity is not AuditLog)
            .ToArray();

        foreach (var entry in changed)
        {
            if (entry.Entity is ITenantEntity tenantEntity)
            {
                if (entry.State == EntityState.Added && tenantEntity.TenantId == Guid.Empty)
                    tenantEntity.TenantId = currentTenant.TenantId ?? throw new InvalidOperationException("Tenant context is required for tenant-owned data.");
                if (entry.State == EntityState.Modified && entry.Property(nameof(ITenantEntity.TenantId)).IsModified)
                    throw new InvalidOperationException("Tenant ownership cannot be changed.");
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now; entry.Entity.CreatedBy = currentUser.UserId; entry.Entity.Version = 1;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now; entry.Entity.UpdatedBy = currentUser.UserId; entry.Entity.Version++;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified; entry.Entity.IsDeleted = true; entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now; entry.Entity.UpdatedBy = currentUser.UserId; entry.Entity.Version++;
                    break;
            }
        }

        foreach (var entry in changed.Where(x => x.Entity is ITenantEntity))
        {
            var tenantId = ((ITenantEntity)entry.Entity).TenantId;
            if (tenantId == Guid.Empty) continue;
            AuditLogs.Add(new AuditLog
            {
                TenantId = tenantId, ActorUserId = currentUser.UserId, Action = entry.State.ToString(),
                EntityType = entry.Metadata.ClrType.Name, EntityId = entry.Entity.Id.ToString(),
                BeforeJson = entry.State == EntityState.Added ? null : Serialize(entry.OriginalValues),
                AfterJson = entry.Entity.IsDeleted ? null : Serialize(entry.CurrentValues), CreatedAt = now, Version = 1
            });
        }

        var result = await base.SaveChangesAsync(cancellationToken);
        if (notificationUserIds.Length > 0)
            await notificationPublisher.PublishChangedAsync(notificationUserIds, cancellationToken);
        return result;
    }

    // Audit records must never become a second credential store.
    private static string Serialize(PropertyValues values) => JsonSerializer.Serialize(values.Properties
        .Where(p => !p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            && !p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)
            && !p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            && !p.Name.Contains("Encrypted", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(p => p.Name, p => values[p]));

    private void ApplyGlobalFilter(ModelBuilder modelBuilder, Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var notDeleted = Expression.Not(Expression.Property(parameter, nameof(AuditableEntity.IsDeleted)));
        Expression body = notDeleted;
        if (typeof(ITenantEntity).IsAssignableFrom(clrType))
        {
            var context = Expression.Constant(this);
            var currentId = Expression.Property(context, nameof(CurrentTenantId));
            var hasValue = Expression.Property(currentId, nameof(Nullable<Guid>.HasValue));
            var value = Expression.Property(currentId, nameof(Nullable<Guid>.Value));
            var entityTenantId = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            body = Expression.AndAlso(notDeleted, Expression.AndAlso(hasValue, Expression.Equal(entityTenantId, value)));
        }
        modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body, parameter));
    }
}
