using Hrms.Domain.Common;

namespace Hrms.Domain;

public sealed class Tenant : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxIdentifier { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    public string Locale { get; set; } = "en-US";
    public TenantStatus Status { get; set; } = TenantStatus.Trial;
    public DateTimeOffset? TrialEndsAt { get; set; }
    public string? LogoUrl { get; set; }
    public string? SettingsJson { get; set; }
}

public sealed class TenantSubscription : TenantEntity
{
    public string PlanCode { get; set; } = "starter";
    public int EmployeeLimit { get; set; } = 50;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UserAccount : TenantEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

public sealed class Role : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string PermissionsCsv { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

public sealed class UserRole : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class RefreshToken : TenantEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}

public sealed class Location : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Department : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? ParentDepartmentId { get; set; }
    public Guid? HeadEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Designation : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Employee : TenantEntity
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? PersonalEmail { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ManagerId { get; set; }
    public decimal BaseSalary { get; set; }
    public string SalaryCurrency { get; set; } = "USD";
    public string? BankAccountMasked { get; set; }
    public string? TaxIdentifierEncrypted { get; set; }
    public string? AddressJson { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed class EmployeeEmergencyContact : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public sealed class EmployeeDocument : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
}

public sealed class StoredDocument : TenantEntity
{
    public DocumentOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public string Category { get; set; } = "attachment";
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public Guid? UploadedByUserId { get; set; }
}

public sealed class UserNotification : TenantEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Kind { get; set; } = "info";
    public string? Link { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class Shift : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public int GraceMinutes { get; set; }
    public bool IsNightShift { get; set; }
}

public sealed class AttendanceRecord : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTimeOffset? ClockedInAt { get; set; }
    public DateTimeOffset? ClockedOutAt { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public decimal WorkHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public TimeOnly? ScheduledStartAt { get; set; }
    public TimeOnly? ScheduledEndAt { get; set; }
    public int? RequiredMinutes { get; set; }
    public int? LateGraceMinutes { get; set; }
    public int? EarlyDepartureGraceMinutes { get; set; }
    public decimal? ClockInLatitude { get; set; }
    public decimal? ClockInLongitude { get; set; }
    public decimal? ClockInAccuracyMeters { get; set; }
    public string? ClockInAddress { get; set; }
    public string? ClockInIpAddress { get; set; }
    public string? ClockInUserAgent { get; set; }
    public decimal? ClockOutLatitude { get; set; }
    public decimal? ClockOutLongitude { get; set; }
    public decimal? ClockOutAccuracyMeters { get; set; }
    public string? ClockOutAddress { get; set; }
    public string? ClockOutIpAddress { get; set; }
    public string? ClockOutUserAgent { get; set; }
}

public sealed class TimesheetEntry : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string? ProjectCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
}

public sealed class Holiday : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public Guid? LocationId { get; set; }
    public bool IsOptional { get; set; }
}

public sealed class LeaveType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal AnnualAllowance { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool RequiresDocument { get; set; }
    public int MaxConsecutiveDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LeaveBalance : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }
    public decimal Pending { get; set; }
    public decimal Available => Entitled - Used - Pending;
}

public sealed class LeaveRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public decimal Days { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
}

public sealed class PayrollRun : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public string Currency { get; set; } = "USD";
    public decimal GrossTotal { get; set; }
    public decimal DeductionTotal { get; set; }
    public decimal NetTotal { get; set; }
}

public sealed class PayrollItem : TenantEntity
{
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal BasicPay { get; set; }
    public decimal Allowances { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Deductions { get; set; }
    public decimal Taxes { get; set; }
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }
    public string? BreakdownJson { get; set; }
}

public sealed class JobOpening : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? HiringManagerId { get; set; }
    public int Openings { get; set; } = 1;
    public string Description { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public DateOnly? ClosesOn { get; set; }
}

public sealed class Candidate : TenantEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ResumeStorageKey { get; set; }
    public string? Source { get; set; }
}

public sealed class JobApplication : TenantEntity
{
    public Guid JobOpeningId { get; set; }
    public Guid CandidateId { get; set; }
    public CandidateStage Stage { get; set; } = CandidateStage.Applied;
    public decimal? Rating { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
}

public sealed class PerformanceCycle : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PerformanceReview : TenantEntity
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ReviewerId { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Draft;
    public decimal? SelfRating { get; set; }
    public decimal? ManagerRating { get; set; }
    public string? GoalsJson { get; set; }
    public string? Feedback { get; set; }
}

public sealed class Asset : TenantEntity
{
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public DateOnly? PurchasedOn { get; set; }
    public decimal? PurchaseCost { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Available;
}

public sealed class AssetAssignment : TenantEntity
{
    public Guid AssetId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class ExpenseClaim : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = string.Empty;
    public string? ReceiptStorageKey { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
    public Guid? ReviewedBy { get; set; }
}

public sealed class TrainingCourse : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Description { get; set; }
    public decimal DurationHours { get; set; }
    public bool IsMandatory { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}

public sealed class TrainingEnrollment : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid EmployeeId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public decimal? Score { get; set; }
}

public sealed class Announcement : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Audience { get; set; } = "all";
}

public sealed class AuditLog : TenantEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class OutboxMessage : TenantEntity
{
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}

public sealed class WorkProject : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? LeadEmployeeId { get; set; }
    public int NextItemNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public sealed class WorkProjectMember : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid EmployeeId { get; set; }
    public bool CanCreateItems { get; set; } = true;
    public bool CanAssignItems { get; set; }
    public bool CanTransitionItems { get; set; } = true;
    public bool CanLogWork { get; set; } = true;
    public bool CanViewAllWorklogs { get; set; }
}

public sealed class WorkItem : TenantEntity
{
    public Guid ProjectId { get; set; }
    public int Number { get; set; }
    public string Key { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? SprintId { get; set; }
    public WorkItemType Type { get; set; } = WorkItemType.Task;
    public string Summary { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Backlog;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public Guid? ReporterEmployeeId { get; set; }
    public Guid? AssigneeEmployeeId { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? OriginalEstimateMinutes { get; set; }
    public int? RemainingEstimateMinutes { get; set; }
    public decimal? StoryPoints { get; set; }
    public string LabelsCsv { get; set; } = string.Empty;
    public WorkItemResolution? Resolution { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

// Provider-neutral, platform-owned email configuration. Credentials are encrypted by
// the application and one superadmin-managed SMTP account serves every company.
public sealed class EmailConfiguration : TenantEntity
{
    public bool IsEnabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public bool UseTls { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string? ReplyToEmail { get; set; }
    public string? ApplicationBaseUrl { get; set; }
}

public sealed class EmailTemplate : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string HtmlTemplate { get; set; } = string.Empty;
    public string? TextTemplate { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsSystem { get; set; } = true;
}

public sealed class EmailOutboxItem : TenantEntity
{
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string ModelJson { get; set; } = "{}";
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class AttendanceCorrection : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid? AttendanceRecordId { get; set; }
    public long? OriginalRecordVersion { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTimeOffset RequestedClockIn { get; set; }
    public DateTimeOffset RequestedClockOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
}

public enum SprintStatus { Planned, Active, Completed }

public sealed class WorkSprint : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public SprintStatus Status { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AttendancePolicy : TenantEntity
{
    public TimeOnly OfficeStartsAt { get; set; } = new(9, 30);
    public TimeOnly OfficeEndsAt { get; set; } = new(18, 30);
    public int RequiredMinutesPerDay { get; set; } = 540;
    public int LateGraceMinutes { get; set; } = 0;
    public int EarlyDepartureGraceMinutes { get; set; } = 0;
    public string WorkingDaysCsv { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
    public bool RequireLocationCapture { get; set; } = true;
}

public sealed class WorkItemAssignee : TenantEntity
{
    public Guid WorkItemId { get; set; }
    public Guid EmployeeId { get; set; }
}

public sealed class WorkItemComment : TenantEntity
{
    public Guid WorkItemId { get; set; }
    public Guid? AuthorEmployeeId { get; set; }
    public string Body { get; set; } = string.Empty;
}

public sealed class WorkLog : TenantEntity
{
    public Guid WorkItemId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int Minutes { get; set; }
    public string? Description { get; set; }
}

public sealed class WorkItemHistory : TenantEntity
{
    public Guid WorkItemId { get; set; }
    public Guid? ActorEmployeeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
}
