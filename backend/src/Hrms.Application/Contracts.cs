using Hrms.Domain;

namespace Hrms.Application;

public sealed record PagedRequest(int Page = 1, int PageSize = 25, string? Search = null)
{
    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, 200);
    public int Skip => (SafePage - 1) * SafePageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record CreateTenantRequest(
    string Name, string Slug, string AdminName, string AdminEmail, string AdminPassword,
    string DefaultCurrency = "USD", string TimeZone = "UTC", int EmployeeLimit = 50);
public sealed record TenantDto(Guid Id, string Name, string Slug, TenantStatus Status, string DefaultCurrency, string TimeZone,
    int EmployeeLimit, DateTimeOffset? TrialEndsAt, string PlanCode, DateTimeOffset SubscriptionStartsAt,
    DateTimeOffset? SubscriptionEndsAt, bool SubscriptionActive, long Version, long SubscriptionVersion);
public sealed record UpdateTenantRequest(string Name, TenantStatus Status, string DefaultCurrency, string TimeZone,
    DateTimeOffset? TrialEndsAt, string PlanCode, int EmployeeLimit, DateTimeOffset SubscriptionStartsAt,
    DateTimeOffset? SubscriptionEndsAt, bool SubscriptionActive, long Version, long SubscriptionVersion);

public sealed record LoginRequest(string TenantSlug, string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record UpdateSelfProfileRequest(string? Phone);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserDto User);
public sealed record UserDto(Guid Id, Guid TenantId, Guid? EmployeeId, string Email, string DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record CreateRoleRequest(string Name, IReadOnlyList<string> Permissions);
public sealed record RoleDto(Guid Id, string Name, IReadOnlyList<string> Permissions, bool IsSystem);
public sealed record CreateUserRequest(string DisplayName, string Email, string Password, IReadOnlyList<Guid> RoleIds, Guid? EmployeeId = null);
public sealed record ProvisionEmployeeAccountRequest(string Password, IReadOnlyList<Guid> RoleIds);
public sealed record UserAdminDto(Guid Id, Guid? EmployeeId, string DisplayName, string Email, bool IsActive, IReadOnlyList<Guid> RoleIds, long Version);
public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds, long Version);
public sealed record ResetUserPasswordRequest(string Password);
public sealed record SetUserActiveRequest(bool IsActive, long Version);

public sealed record CreateEmployeeRequest(
    string EmployeeNumber, string FirstName, string LastName, string WorkEmail, DateOnly HireDate,
    EmploymentType EmploymentType = EmploymentType.Permanent, Guid? DepartmentId = null,
    Guid? DesignationId = null, Guid? LocationId = null, Guid? ManagerId = null,
    decimal BaseSalary = 0, string SalaryCurrency = "USD", string? Phone = null);
public sealed record UpdateEmployeeRequest(
    string EmployeeNumber, string FirstName, string LastName, string WorkEmail, string? Phone, DateOnly HireDate, EmploymentStatus Status,
    EmploymentType EmploymentType, Guid? DepartmentId, Guid? DesignationId, Guid? LocationId,
    Guid? ManagerId, decimal BaseSalary, string SalaryCurrency, long Version);
public sealed record EmployeeDto(
    Guid Id, string EmployeeNumber, string FirstName, string LastName, string FullName, string WorkEmail, string? Phone,
    DateOnly HireDate, EmploymentStatus Status, EmploymentType EmploymentType, Guid? DepartmentId,
    Guid? DesignationId, Guid? LocationId, Guid? ManagerId, decimal BaseSalary,
    string SalaryCurrency, Guid? UserId, long Version);
public sealed record LoginHistoryDto(DateTimeOffset LoggedInAt, string? IpAddress, string? UserAgent);

public sealed record CreateDepartmentRequest(string Name, string Code, Guid? ParentDepartmentId = null);
public sealed record DepartmentDto(Guid Id, string Name, string Code, Guid? ParentDepartmentId, Guid? HeadEmployeeId, bool IsActive);
public sealed record CreateDesignationRequest(string Name, string Code, int Level, string? Description = null);
public sealed record DesignationDto(Guid Id, string Name, string Code, int Level, string? Description, bool IsActive);
public sealed record CreateLocationRequest(string Name, string Code, string? Address = null, string? City = null, string? CountryCode = null);
public sealed record LocationDto(Guid Id, string Name, string Code, string? Address, string? City, string? CountryCode, bool IsActive);

public sealed record CreateLeaveTypeRequest(string Name, string Code, decimal AnnualAllowance, bool IsPaid = true, bool RequiresDocument = false, int MaxConsecutiveDays = 0);
public sealed record LeaveTypeDto(Guid Id, string Name, string Code, decimal AnnualAllowance, bool IsPaid, bool RequiresDocument, int MaxConsecutiveDays);
public sealed record SubmitLeaveRequest(Guid EmployeeId, Guid LeaveTypeId, DateOnly StartsOn, DateOnly EndsOn, decimal Days, string Reason);
public sealed record ReviewLeaveRequest(bool Approve, string? Comment, long Version);
public sealed record LeaveRequestDto(Guid Id, Guid EmployeeId, Guid LeaveTypeId, DateOnly StartsOn, DateOnly EndsOn, decimal Days, string Reason, LeaveRequestStatus Status, string? ReviewComment, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt, long Version);
public sealed record LeaveBalanceDto(Guid LeaveTypeId, int Year, decimal Entitled, decimal Used, decimal Pending, decimal Available);

public sealed record ClockRequest(Guid EmployeeId, DateTimeOffset? Timestamp = null, string Source = "web", string? Notes = null,
    decimal? Latitude = null, decimal? Longitude = null, decimal? AccuracyMeters = null, string? Address = null,
    string? IpAddress = null, string? UserAgent = null);
public sealed record SelfClockRequest(decimal? Latitude = null, decimal? Longitude = null, decimal? AccuracyMeters = null, string? Address = null, string Source = "web", string? Notes = null);
public sealed record AttendanceDto(Guid Id, Guid EmployeeId, DateOnly WorkDate, DateTimeOffset? ClockedInAt, DateTimeOffset? ClockedOutAt,
    AttendanceStatus Status, string SessionState, decimal WorkHours, decimal OvertimeHours, string? Source, string? Notes,
    decimal? ClockInLatitude, decimal? ClockInLongitude, decimal? ClockInAccuracyMeters, string? ClockInAddress, string? ClockInIpAddress, string? ClockInUserAgent,
    decimal? ClockOutLatitude, decimal? ClockOutLongitude, decimal? ClockOutAccuracyMeters, string? ClockOutAddress, string? ClockOutIpAddress, string? ClockOutUserAgent, long Version);
public sealed record AttendancePolicyDto(Guid? Id, TimeOnly OfficeStartsAt, TimeOnly OfficeEndsAt, string TotalWorkHours,
    int LateGraceMinutes, int EarlyDepartureGraceMinutes, IReadOnlyList<string> WorkingDays, bool RequireLocationCapture, long Version);
public sealed record UpdateAttendancePolicyRequest(TimeOnly OfficeStartsAt, TimeOnly OfficeEndsAt,
    int LateGraceMinutes, int EarlyDepartureGraceMinutes, IReadOnlyList<string> WorkingDays, bool RequireLocationCapture, long Version = 0);
public sealed record DailyAttendanceReportDto(Guid EmployeeId, string EmployeeName, DateOnly WorkDate, DateTimeOffset? FirstCheckIn, DateTimeOffset? LastCheckOut,
    decimal TotalHours, decimal RequiredHours, int LateMinutes, int EarlyDepartureMinutes, decimal OvertimeHours,
    decimal ShortfallHours, int SessionCount, string Status);
public sealed record AttendanceSummaryDto(Guid EmployeeId, string EmployeeName, DateOnly From, DateOnly To, int ScheduledDays,
    int DaysPresent, int DaysAbsent, int DaysOnLeave, decimal TotalHours, decimal AverageHours,
    int LateDays, int EarlyDepartureDays, int ShortDays, decimal OvertimeHours);
public sealed record CreateShiftRequest(string Name, TimeOnly StartsAt, TimeOnly EndsAt, int GraceMinutes = 0, bool IsNightShift = false);
public sealed record ShiftDto(Guid Id, string Name, TimeOnly StartsAt, TimeOnly EndsAt, int GraceMinutes, bool IsNightShift);
public sealed record CreateHolidayRequest(string Name, DateOnly Date, Guid? LocationId = null, bool IsOptional = false);
public sealed record HolidayDto(Guid Id, string Name, DateOnly Date, Guid? LocationId, bool IsOptional);
public sealed record SubmitTimesheetRequest(Guid EmployeeId, DateOnly WorkDate, string Description, decimal Hours, string? ProjectCode = null);
public sealed record ReviewTimesheetRequest(bool Approve, long Version);
public sealed record TimesheetDto(Guid Id, Guid EmployeeId, DateOnly WorkDate, string? ProjectCode, string Description, decimal Hours, WorkflowStatus Status, long Version);
public sealed record AddEmployeeDocumentRequest(Guid EmployeeId, string DocumentType, string FileName, string StorageKey, string? ContentType = null, DateOnly? ExpiresOn = null);
public sealed record VerifyDocumentRequest(DocumentStatus Status, long Version);
public sealed record EmployeeDocumentDto(Guid Id, Guid EmployeeId, string DocumentType, string FileName, string StorageKey, string? ContentType, DateOnly? ExpiresOn, DocumentStatus Status, long Version);
public sealed record CreateAnnouncementRequest(string Title, string Body, DateTimeOffset? ExpiresAt = null, string Audience = "all");
public sealed record AnnouncementDto(Guid Id, string Title, string Body, DateTimeOffset PublishedAt, DateTimeOffset? ExpiresAt, string Audience);

public sealed record CreatePayrollRunRequest(string Name, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly PaymentDate, string Currency = "USD");
public sealed record PayrollRunDto(Guid Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly PaymentDate, PayrollRunStatus Status, string Currency, decimal GrossTotal, decimal DeductionTotal, decimal NetTotal, long Version);
public sealed record PayrollItemDto(Guid Id, Guid EmployeeId, decimal BasicPay, decimal Allowances, decimal OvertimePay, decimal Deductions, decimal Taxes, decimal GrossPay, decimal NetPay);

public sealed record CreateJobRequest(string Title, string Code, string Description, Guid? DepartmentId = null, Guid? HiringManagerId = null, int Openings = 1, DateOnly? ClosesOn = null);
public sealed record CreateCandidateRequest(string FirstName, string LastName, string Email, string? Phone = null, string? ResumeStorageKey = null, string? Source = null);
public sealed record ApplyCandidateRequest(Guid JobOpeningId, Guid CandidateId);
public sealed record MoveCandidateRequest(CandidateStage Stage, decimal? Rating = null, string? Notes = null);
public sealed record JobDto(Guid Id, string Title, string Code, int Openings, JobStatus Status, DateOnly? ClosesOn);
public sealed record CandidateDto(Guid Id, string FirstName, string LastName, string Email, string? Phone, string? Source);
public sealed record JobApplicationDto(Guid Id, Guid JobOpeningId, Guid CandidateId, CandidateStage Stage, decimal? Rating, string? Notes, DateTimeOffset AppliedAt);

public sealed record CreatePerformanceCycleRequest(string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record CreateReviewRequest(Guid CycleId, Guid EmployeeId, Guid ReviewerId, string? GoalsJson = null);
public sealed record UpdateReviewRequest(ReviewStatus Status, decimal? SelfRating, decimal? ManagerRating, string? Feedback, long Version);
public sealed record PerformanceReviewDto(Guid Id, Guid CycleId, Guid EmployeeId, Guid ReviewerId, ReviewStatus Status, decimal? SelfRating, decimal? ManagerRating, string? GoalsJson, string? Feedback, long Version);

public sealed record CreateAssetRequest(string AssetTag, string Name, string Category, string? SerialNumber = null, DateOnly? PurchasedOn = null, decimal? PurchaseCost = null);
public sealed record AssignAssetRequest(Guid EmployeeId, string? Notes = null);
public sealed record AssetDto(Guid Id, string AssetTag, string Name, string Category, string? SerialNumber, AssetStatus Status);

public sealed record CreateExpenseRequest(Guid EmployeeId, string Category, DateOnly ExpenseDate, decimal Amount, string Currency, string Description, string? ReceiptStorageKey = null);
public sealed record ReviewExpenseRequest(bool Approve, long Version);
public sealed record ExpenseDto(Guid Id, Guid EmployeeId, string ClaimNumber, string Category, DateOnly ExpenseDate, decimal Amount, string Currency, string Description, ExpenseStatus Status, long Version);

public sealed record CreateCourseRequest(string Title, string? Provider, string? Description, decimal DurationHours, bool IsMandatory, DateOnly? ExpiresOn);
public sealed record EnrollEmployeeRequest(Guid EmployeeId);
public sealed record CompleteTrainingRequest(decimal? Score, long Version);
public sealed record TrainingCourseDto(Guid Id, string Title, string? Provider, decimal DurationHours, bool IsMandatory, DateOnly? ExpiresOn);
public sealed record TrainingEnrollmentDto(Guid Id, Guid CourseId, Guid EmployeeId, EnrollmentStatus Status, DateTimeOffset EnrolledAt, DateTimeOffset? CompletedAt, decimal? Score, long Version);

public sealed record DashboardDto(int ActiveEmployees, int PendingLeaveRequests, int OpenJobs, int AvailableAssets, decimal CurrentPayrollTotal, IReadOnlyDictionary<string, int> EmployeesByStatus);
public sealed record AuditLogDto(Guid Id, DateTimeOffset CreatedAt, Guid? ActorUserId, string Action, string EntityType, string? EntityId, string? IpAddress, string? CorrelationId);

public sealed record SelfProfileDto(Guid EmployeeId, string EmployeeNumber, string FullName, string WorkEmail, string? Phone, DateOnly HireDate,
    EmploymentStatus Status, EmploymentType EmploymentType, Guid? DepartmentId, Guid? DesignationId, Guid? LocationId, Guid? ManagerId,
    string SalaryCurrency, decimal BaseSalary);
public sealed record SelfDashboardDto(SelfProfileDto Profile, AttendanceDto? TodayAttendance, decimal TodayTotalHours, int TodaySessionCount, bool RequireLocationCapture, int PendingLeaveRequests, decimal AvailableLeaveDays,
    int PendingTimesheets, int OpenExpenses, int TrainingDue, IReadOnlyList<AnnouncementDto> Announcements);
public sealed record SelfLeaveRequest(Guid LeaveTypeId, DateOnly StartsOn, DateOnly EndsOn, decimal Days, string Reason);
public sealed record SelfTimesheetRequest(DateOnly WorkDate, string Description, decimal Hours, string? ProjectCode = null);
public sealed record SelfExpenseRequest(string Category, DateOnly ExpenseDate, decimal Amount, string Currency, string Description, string? ReceiptStorageKey = null);
public sealed record PayslipDto(Guid PayrollRunId, string RunName, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly PaymentDate, string Currency,
    decimal BasicPay, decimal Allowances, decimal OvertimePay, decimal Deductions, decimal Taxes, decimal GrossPay, decimal NetPay, PayrollRunStatus Status);
public sealed record TeamMemberDto(Guid Id, string EmployeeNumber, string FullName, string WorkEmail, EmploymentStatus Status, Guid? DepartmentId, Guid? DesignationId);
