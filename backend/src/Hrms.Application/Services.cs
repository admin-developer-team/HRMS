using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public interface ITenantService
{
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);
    Task<PagedResult<TenantDto>> SearchAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken);
}

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(RefreshRequest request, CancellationToken cancellationToken);
}

public interface IIdentityAdminService
{
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken);
    Task<UserAdminDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserAdminDto> ProvisionEmployeeAsync(Guid employeeId, ProvisionEmployeeAccountRequest request, CancellationToken cancellationToken);
    Task<UserAdminDto> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken);
    Task<UserAdminDto> ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken);
    Task<UserAdminDto> SetActiveAsync(Guid userId, SetUserActiveRequest request, CancellationToken cancellationToken);
    Task<PagedResult<UserAdminDto>> SearchUsersAsync(PagedRequest request, CancellationToken cancellationToken);
}

public interface IEmployeeService
{
    Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<EmployeeDto>> SearchAsync(PagedRequest request, EmploymentStatus? status, Guid? departmentId, CancellationToken cancellationToken);
    Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoginHistoryDto>> GetLoginHistoryAsync(Guid id, int take, CancellationToken cancellationToken);
}

public interface IOrganizationService
{
    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken cancellationToken);
    Task<DesignationDto> CreateDesignationAsync(CreateDesignationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DesignationDto>> ListDesignationsAsync(CancellationToken cancellationToken);
    Task<LocationDto> CreateLocationAsync(CreateLocationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationDto>> ListLocationsAsync(CancellationToken cancellationToken);
}

public interface ILeaveService
{
    Task<LeaveTypeDto> CreateTypeAsync(CreateLeaveTypeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveTypeDto>> ListTypesAsync(CancellationToken cancellationToken);
    Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken cancellationToken);
    Task<LeaveRequestDto> ReviewAsync(Guid id, ReviewLeaveRequest request, CancellationToken cancellationToken);
    Task<LeaveRequestDto> CancelAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<PagedResult<LeaveRequestDto>> SearchAsync(PagedRequest request, Guid? employeeId, LeaveRequestStatus? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(Guid employeeId, int year, CancellationToken cancellationToken);
}

public interface IAttendanceService
{
    Task<AttendanceDto> ClockInAsync(ClockRequest request, CancellationToken cancellationToken);
    Task<AttendanceDto> ClockOutAsync(ClockRequest request, CancellationToken cancellationToken);
    Task<PagedResult<AttendanceDto>> SearchAsync(PagedRequest request, Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendancePolicyDto>> GetPolicyAsync(CancellationToken cancellationToken);
    Task<AttendancePolicyDto> UpdatePolicyAsync(UpdateAttendancePolicyRequest request, CancellationToken cancellationToken);
    Task<PagedResult<DailyAttendanceReportDto>> ReportAsync(PagedRequest request, Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceSummaryDto>> SummaryAsync(Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}

public interface IWorkforceOperationsService
{
    Task<ShiftDto> CreateShiftAsync(CreateShiftRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShiftDto>> ListShiftsAsync(CancellationToken cancellationToken);
    Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(int year, CancellationToken cancellationToken);
    Task<TimesheetDto> SubmitTimesheetAsync(SubmitTimesheetRequest request, CancellationToken cancellationToken);
    Task<TimesheetDto> ReviewTimesheetAsync(Guid id, ReviewTimesheetRequest request, CancellationToken cancellationToken);
    Task<PagedResult<TimesheetDto>> SearchTimesheetsAsync(PagedRequest request, Guid? employeeId, WorkflowStatus? status, CancellationToken cancellationToken);
    Task<EmployeeDocumentDto> AddDocumentAsync(AddEmployeeDocumentRequest request, CancellationToken cancellationToken);
    Task<EmployeeDocumentDto> VerifyDocumentAsync(Guid id, VerifyDocumentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeDocumentDto>> ListDocumentsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(CancellationToken cancellationToken);
}

public interface IPayrollService
{
    Task<PayrollRunDto> CreateAsync(CreatePayrollRunRequest request, CancellationToken cancellationToken);
    Task<PayrollRunDto> CalculateAsync(Guid id, CancellationToken cancellationToken);
    Task<PayrollRunDto> ChangeStatusAsync(Guid id, PayrollRunStatus status, long version, CancellationToken cancellationToken);
    Task<PagedResult<PayrollRunDto>> SearchAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PayrollItemDto>> GetItemsAsync(Guid runId, CancellationToken cancellationToken);
}

public interface IRecruitmentService
{
    Task<JobDto> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken);
    Task<CandidateDto> CreateCandidateAsync(CreateCandidateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CandidateDto>> ListCandidatesAsync(CancellationToken cancellationToken);
    Task<JobApplicationDto> ApplyAsync(ApplyCandidateRequest request, CancellationToken cancellationToken);
    Task<JobApplicationDto> MoveAsync(Guid applicationId, MoveCandidateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<JobDto>> ListJobsAsync(JobStatus? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<JobApplicationDto>> ListApplicationsAsync(Guid jobId, CancellationToken cancellationToken);
}

public interface IPerformanceService
{
    Task<Guid> CreateCycleAsync(CreatePerformanceCycleRequest request, CancellationToken cancellationToken);
    Task<PerformanceReviewDto> CreateReviewAsync(CreateReviewRequest request, CancellationToken cancellationToken);
    Task<PerformanceReviewDto> UpdateReviewAsync(Guid id, UpdateReviewRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PerformanceReviewDto>> ListReviewsAsync(Guid? employeeId, Guid? cycleId, CancellationToken cancellationToken);
}

public interface IAssetService
{
    Task<AssetDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken);
    Task<AssetDto> AssignAsync(Guid assetId, AssignAssetRequest request, CancellationToken cancellationToken);
    Task<AssetDto> ReturnAsync(Guid assetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetDto>> ListAsync(AssetStatus? status, CancellationToken cancellationToken);
}

public interface IExpenseService
{
    Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<ExpenseDto> SubmitAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<ExpenseDto> ReviewAsync(Guid id, ReviewExpenseRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ExpenseDto>> SearchAsync(PagedRequest request, Guid? employeeId, ExpenseStatus? status, CancellationToken cancellationToken);
}

public interface ITrainingService
{
    Task<TrainingCourseDto> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken);
    Task<TrainingEnrollmentDto> EnrollAsync(Guid courseId, EnrollEmployeeRequest request, CancellationToken cancellationToken);
    Task<TrainingEnrollmentDto> CompleteAsync(Guid enrollmentId, CompleteTrainingRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingCourseDto>> ListCoursesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingEnrollmentDto>> ListEnrollmentsAsync(Guid? employeeId, CancellationToken cancellationToken);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
}

public interface ISelfService
{
    Task<SelfProfileDto> GetProfileAsync(CancellationToken cancellationToken);
    Task<SelfProfileDto> UpdateProfileAsync(UpdateSelfProfileRequest request, CancellationToken cancellationToken);
    Task<SelfDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AttendanceDto> ClockInAsync(SelfClockRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<AttendanceDto> ClockOutAsync(SelfClockRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<PagedResult<AttendanceDto>> GetAttendanceAsync(PagedRequest request, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<PagedResult<DailyAttendanceReportDto>> GetAttendanceReportAsync(PagedRequest request, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceSummaryDto>> GetAttendanceSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<LeaveRequestDto> SubmitLeaveAsync(SelfLeaveRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken cancellationToken);
    Task<PagedResult<LeaveRequestDto>> GetLeaveAsync(PagedRequest request, LeaveRequestStatus? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveBalanceDto>> GetLeaveBalancesAsync(int year, CancellationToken cancellationToken);
    Task<TimesheetDto> SubmitTimesheetAsync(SelfTimesheetRequest request, CancellationToken cancellationToken);
    Task<PagedResult<TimesheetDto>> GetTimesheetsAsync(PagedRequest request, WorkflowStatus? status, CancellationToken cancellationToken);
    Task<ExpenseDto> CreateExpenseAsync(SelfExpenseRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ExpenseDto>> GetExpensesAsync(PagedRequest request, ExpenseStatus? status, CancellationToken cancellationToken);
    Task<ExpenseDto> SubmitExpenseAsync(Guid id, long version, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingEnrollmentDto>> GetTrainingAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingCourseDto>> GetTrainingCoursesAsync(CancellationToken cancellationToken);
    Task<TrainingEnrollmentDto> CompleteTrainingAsync(Guid id, CompleteTrainingRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PerformanceReviewDto>> GetPerformanceAsync(CancellationToken cancellationToken);
    Task<PerformanceReviewDto> UpdatePerformanceAsync(Guid id, UpdateReviewRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetDto>> GetAssetsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PayslipDto>> GetPayslipsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TeamMemberDto>> GetTeamAsync(CancellationToken cancellationToken);
    Task<PagedResult<LeaveRequestDto>> GetTeamLeaveAsync(PagedRequest request, LeaveRequestStatus? status, CancellationToken cancellationToken);
    Task<LeaveRequestDto> ReviewTeamLeaveAsync(Guid id, ReviewLeaveRequest request, CancellationToken cancellationToken);
    Task<PagedResult<AttendanceDto>> GetTeamAttendanceAsync(PagedRequest request, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<PagedResult<TimesheetDto>> GetTeamTimesheetsAsync(PagedRequest request, WorkflowStatus? status, CancellationToken cancellationToken);
    Task<TimesheetDto> ReviewTeamTimesheetAsync(Guid id, ReviewTimesheetRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ExpenseDto>> GetTeamExpensesAsync(PagedRequest request, ExpenseStatus? status, CancellationToken cancellationToken);
    Task<ExpenseDto> ReviewTeamExpenseAsync(Guid id, ReviewExpenseRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PerformanceReviewDto>> GetTeamPerformanceAsync(CancellationToken cancellationToken);
    Task<PerformanceReviewDto> UpdateTeamPerformanceAsync(Guid id, UpdateReviewRequest request, CancellationToken cancellationToken);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);
}

public abstract class ServiceBase(ICurrentTenant tenant)
{
    protected Guid TenantId => tenant.TenantId ?? throw new DomainException("A valid tenant context is required.");
    protected static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"{name} is required.");
    }
    protected static void CheckVersion(AuditableEntity entity, long version)
    {
        if (entity.Version != version) throw new DomainException("The record was changed by another user. Reload it and retry.");
    }
}

public sealed class TenantService(
    IRepository<Tenant> tenants, IRepository<TenantSubscription> subscriptions, IRepository<UserAccount> users,
    IRepository<Role> roles, IRepository<UserRole> userRoles, IRepository<LeaveType> leaveTypes,
    IRepository<Employee> employees, IRepository<RefreshToken> refreshTokens,
    IPasswordHasher passwordHasher, IUnitOfWork unitOfWork, ICurrentTenant currentTenant) : ITenantService
{
    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (slug.Length < 3 || slug.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            throw new DomainException("Slug must be at least 3 characters and contain only letters, numbers, or hyphens.");
        if (request.AdminPassword.Length < 8) throw new DomainException("Admin password must be at least 8 characters.");
        if (await tenants.AnyAsync(x => x.Slug == slug, ct)) throw new DomainException("Tenant slug is already in use.");

        var tenant = new Tenant
        {
            Name = request.Name.Trim(), Slug = slug, DefaultCurrency = request.DefaultCurrency.ToUpperInvariant(),
            TimeZone = request.TimeZone, Status = TenantStatus.Trial, TrialEndsAt = DateTimeOffset.UtcNow.AddDays(30)
        };
        await tenants.AddAsync(tenant, ct);
        currentTenant.Set(tenant.Id, tenant.Slug);
        var role = new Role
        {
            TenantId = tenant.Id, Name = "Tenant Administrator", NormalizedName = "TENANT_ADMIN",
            PermissionsCsv = "*", IsSystem = true
        };
        var admin = new UserAccount
        {
            TenantId = tenant.Id, Email = request.AdminEmail.Trim().ToLowerInvariant(), DisplayName = request.AdminName.Trim(),
            PasswordHash = passwordHasher.Hash(request.AdminPassword), IsActive = true
        };
        var subscription = new TenantSubscription
        {
            TenantId = tenant.Id, PlanCode = "trial", EmployeeLimit = Math.Max(1, request.EmployeeLimit),
            StartsAt = DateTimeOffset.UtcNow, EndsAt = tenant.TrialEndsAt
        };
        await subscriptions.AddAsync(subscription, ct);
        await roles.AddAsync(role, ct);
        foreach (var definition in Permissions.TenantSystemRoles)
            await roles.AddAsync(new Role { TenantId = tenant.Id, Name = definition.Name, NormalizedName = definition.NormalizedName, PermissionsCsv = string.Join(',', definition.Permissions), IsSystem = true }, ct);
        await users.AddAsync(admin, ct);
        await userRoles.AddAsync(new UserRole { TenantId = tenant.Id, UserId = admin.Id, RoleId = role.Id }, ct);
        await leaveTypes.AddAsync(new LeaveType { TenantId = tenant.Id, Name = "Annual Leave", Code = "ANNUAL", AnnualAllowance = 20, IsPaid = true }, ct);
        await leaveTypes.AddAsync(new LeaveType { TenantId = tenant.Id, Name = "Sick Leave", Code = "SICK", AnnualAllowance = 10, IsPaid = true }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(tenant, subscription);
    }

    public async Task<PagedResult<TenantDto>> SearchAsync(PagedRequest request, CancellationToken ct)
    {
        var q = request.Search?.Trim().ToLowerInvariant();
        System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate = x => x.Slug != "platform" &&
            (string.IsNullOrEmpty(q) || x.Name.ToLower().Contains(q) || x.Slug.Contains(q));
        var total = await tenants.CountAsync(predicate, ct);
        var rows = await tenants.ListAsync(predicate, x => x.OrderBy(t => t.Name), request.Skip, request.SafePageSize, ct);
        var result = new List<TenantDto>();
        foreach (var t in rows)
        {
            currentTenant.Set(t.Id, t.Slug);
            var sub = (await subscriptions.ListAsync(orderBy: q => q.OrderByDescending(x => x.CreatedAt), take: 1, cancellationToken: ct)).FirstOrDefault();
            result.Add(Map(t, sub));
        }
        currentTenant.Clear();
        return new PagedResult<TenantDto>(result, request.SafePage, request.SafePageSize, total);
    }

    public async Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest r, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Customer company not found.");
        if (tenant.Slug == "platform") throw new DomainException("The platform tenant cannot be managed as a customer company.");
        CheckVersion(tenant, r.Version);
        Required(r.Name, "Company name"); Required(r.DefaultCurrency, "Currency"); Required(r.TimeZone, "Time zone"); Required(r.PlanCode, "Plan");
        if (r.DefaultCurrency.Length != 3 || !r.DefaultCurrency.All(char.IsAsciiLetter)) throw new DomainException("Use a three-letter currency code.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone); } catch (TimeZoneNotFoundException) { throw new DomainException("Time zone is invalid."); } catch (InvalidTimeZoneException) { throw new DomainException("Time zone is invalid."); }
        if (!Enum.IsDefined(r.Status)) throw new DomainException("Company status is invalid.");
        if (r.Status == TenantStatus.Trial && (!r.TrialEndsAt.HasValue || r.TrialEndsAt <= DateTimeOffset.UtcNow)) throw new DomainException("An active trial needs a future trial end date.");
        if (r.EmployeeLimit < 1) throw new DomainException("Employee limit must be at least 1.");
        if (r.SubscriptionEndsAt.HasValue && r.SubscriptionEndsAt < r.SubscriptionStartsAt) throw new DomainException("Subscription end date cannot be before its start date.");

        currentTenant.Set(tenant.Id, tenant.Slug);
        try
        {
            var subscription = (await subscriptions.ListAsync(orderBy: q => q.OrderByDescending(x => x.CreatedAt), take: 1, cancellationToken: ct)).FirstOrDefault()
                ?? throw new DomainException("The company does not have a subscription record.");
            CheckVersion(subscription, r.SubscriptionVersion);
            var licensedEmployees = await employees.CountAsync(x => x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod, ct);
            if (r.EmployeeLimit < licensedEmployees) throw new DomainException($"Employee limit cannot be lower than the {licensedEmployees} currently active employees.");

            tenant.Name = r.Name.Trim(); tenant.Status = r.Status; tenant.DefaultCurrency = r.DefaultCurrency.ToUpperInvariant();
            tenant.TimeZone = r.TimeZone.Trim(); tenant.TrialEndsAt = r.Status == TenantStatus.Trial ? r.TrialEndsAt?.ToUniversalTime() : null;
            subscription.PlanCode = r.PlanCode.Trim().ToLowerInvariant(); subscription.EmployeeLimit = r.EmployeeLimit;
            subscription.StartsAt = r.SubscriptionStartsAt.ToUniversalTime(); subscription.EndsAt = r.SubscriptionEndsAt?.ToUniversalTime(); subscription.IsActive = r.SubscriptionActive;
            if (r.Status is TenantStatus.Suspended or TenantStatus.Cancelled)
            {
                var userIds = (await users.ListAsync(cancellationToken: ct)).Select(x => x.Id).ToHashSet();
                foreach (var token in await refreshTokens.ListAsync(x => userIds.Contains(x.UserId) && x.RevokedAt == null, cancellationToken: ct)) token.RevokedAt = DateTimeOffset.UtcNow;
            }
            await unitOfWork.SaveChangesAsync(ct);
            return Map(tenant, subscription);
        }
        finally { currentTenant.Clear(); }
    }

    private static TenantDto Map(Tenant tenant, TenantSubscription? subscription) => new(
        tenant.Id, tenant.Name, tenant.Slug, tenant.Status, tenant.DefaultCurrency, tenant.TimeZone,
        subscription?.EmployeeLimit ?? 0, tenant.TrialEndsAt, subscription?.PlanCode ?? "unassigned",
        subscription?.StartsAt ?? tenant.CreatedAt, subscription?.EndsAt, subscription?.IsActive ?? false,
        tenant.Version, subscription?.Version ?? 0);
    private static void Required(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"{name} is required."); }
    private static void CheckVersion(AuditableEntity entity, long version) { if (entity.Version != version) throw new DomainException("The record was changed by another user. Reload it and retry."); }
}

public sealed class AuthService(
    IRepository<Tenant> tenants, IRepository<UserAccount> users, IRepository<Role> roles, IRepository<UserRole> userRoles,
    IRepository<RefreshToken> refreshTokens, IRepository<Employee> employees, IRepository<TenantSubscription> subscriptions, IRepository<AuditLog> auditLogs, ICurrentTenant currentTenant, IPasswordHasher passwordHasher,
    ITokenService tokenService, IUnitOfWork unitOfWork) : IAuthService
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var tenant = await tenants.FirstOrDefaultAsync(x => x.Slug == slug && (x.Status == TenantStatus.Active || (x.Status == TenantStatus.Trial && x.TrialEndsAt > now)), ct)
            ?? throw new DomainException("Invalid tenant or credentials.");
        currentTenant.Set(tenant.Id, tenant.Slug);
        if (tenant.Slug != "platform")
        {
            var subscription = await subscriptions.FirstOrDefaultAsync(x => x.IsActive && x.StartsAt <= now && (!x.EndsAt.HasValue || x.EndsAt > now), ct);
            if (subscription is null) throw new DomainException("This company subscription is inactive or expired. Contact the platform administrator.");
        }
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.FirstOrDefaultAsync(x => x.Email == email, ct)
            ?? throw new DomainException("Invalid tenant or credentials.");
        if (!user.IsActive || user.LockedUntil > DateTimeOffset.UtcNow) throw new DomainException("Account is inactive or locked.");
        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5) user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);
            await unitOfWork.SaveChangesAsync(ct);
            throw new DomainException("Invalid tenant or credentials.");
        }
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await auditLogs.AddAsync(new AuditLog
        {
            TenantId = tenant.Id,
            ActorUserId = user.Id,
            Action = "Login",
            EntityType = nameof(UserAccount),
            EntityId = user.Id.ToString(),
            IpAddress = ipAddress,
            AfterJson = System.Text.Json.JsonSerializer.Serialize(new { UserAgent = userAgent }),
            CreatedAt = DateTimeOffset.UtcNow,
            Version = 1
        }, ct);
        return await IssueAsync(user, ct);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (currentTenant.TenantId is null) throw new DomainException("X-Tenant-ID is required to refresh a token.");
        var now = DateTimeOffset.UtcNow;
        var tenant = await tenants.GetByIdAsync(currentTenant.TenantId.Value, ct)
            ?? throw new DomainException("Company is unavailable.");
        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Trial) || (tenant.Status == TenantStatus.Trial && tenant.TrialEndsAt <= now))
            throw new DomainException("Company access is suspended, cancelled, or expired.");
        if (tenant.Slug != "platform" && !await subscriptions.AnyAsync(x => x.IsActive && x.StartsAt <= now && (!x.EndsAt.HasValue || x.EndsAt > now), ct))
            throw new DomainException("This company subscription is inactive or expired. Contact the platform administrator.");
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await refreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive) throw new DomainException("Refresh token is invalid or expired.");
        var user = await users.GetByIdAsync(stored.UserId, ct) ?? throw new DomainException("User no longer exists.");
        if (!user.IsActive || user.LockedUntil > DateTimeOffset.UtcNow) throw new DomainException("Account is inactive or locked.");
        return await IssueAsync(user, ct, stored);
    }

    public async Task RevokeAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await refreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (stored is not null) { stored.RevokedAt = DateTimeOffset.UtcNow; await unitOfWork.SaveChangesAsync(ct); }
    }

    private async Task<TokenResponse> IssueAsync(UserAccount user, CancellationToken ct, RefreshToken? replacedToken = null)
    {
        var links = await userRoles.ListAsync(x => x.UserId == user.Id, cancellationToken: ct);
        var roleIds = links.Select(x => x.RoleId).ToHashSet();
        var assigned = await roles.ListAsync(x => roleIds.Contains(x.Id), cancellationToken: ct);
        var roleNames = assigned.Select(x => x.NormalizedName).Distinct().ToArray();
        var permissions = assigned.SelectMany(x => x.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToArray();
        var employeeId = (await employees.FirstOrDefaultAsync(x => x.UserId == user.Id, ct))?.Id;
        var sessionId = Guid.NewGuid();
        var access = tokenService.CreateAccessToken(user.Id, user.TenantId, employeeId, user.Email, user.IsPlatformAdmin, roleNames, permissions, sessionId);
        var refresh = tokenService.CreateRefreshToken();
        if (replacedToken is not null)
        {
            replacedToken.RevokedAt = DateTimeOffset.UtcNow;
            replacedToken.ReplacedByHash = tokenService.HashRefreshToken(refresh);
        }
        await refreshTokens.AddAsync(new RefreshToken
        {
            Id = sessionId, TenantId = user.TenantId, UserId = user.Id, TokenHash = tokenService.HashRefreshToken(refresh),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(tokenService.RefreshTokenLifetimeDays)
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new TokenResponse(access, refresh, DateTimeOffset.UtcNow.AddMinutes(tokenService.AccessTokenLifetimeMinutes), new UserDto(user.Id, user.TenantId, employeeId, user.Email, user.DisplayName, roleNames, permissions));
    }
}

public sealed class IdentityAdminService(IRepository<UserAccount> users, IRepository<Role> roles, IRepository<UserRole> userRoles, IRepository<Employee> employees, IRepository<RefreshToken> refreshTokens, IPasswordHasher passwordHasher, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications, ICurrentUser actor) : ServiceBase(tenant), IIdentityAdminService
{
    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest r, CancellationToken ct)
    {
        var normalized = r.Name.Trim().Replace(' ', '_').ToUpperInvariant();
        if (await roles.AnyAsync(x => x.NormalizedName == normalized, ct)) throw new DomainException("Role name already exists.");
        var requested = r.Permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ValidateDelegation(requested);
        if (requested.Any(x => x != Permissions.All && !Permissions.Catalog.Contains(x, StringComparer.OrdinalIgnoreCase))) throw new DomainException("One or more permissions are not supported.");
        var role = new Role { TenantId = TenantId, Name = r.Name.Trim(), NormalizedName = normalized, PermissionsCsv = string.Join(',', requested) };
        await roles.AddAsync(role, ct); await unitOfWork.SaveChangesAsync(ct); return Map(role);
    }
    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct) => (await roles.ListAsync(orderBy: q => q.OrderBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<UserAdminDto> CreateUserAsync(CreateUserRequest r, CancellationToken ct)
    {
        if (r.Password.Length < 8) throw new DomainException("Password must be at least 8 characters.");
        var email = r.Email.Trim().ToLowerInvariant(); if (await users.AnyAsync(x => x.Email == email, ct)) throw new DomainException("User email already exists.");
        var validRoles = await roles.ListAsync(x => r.RoleIds.Contains(x.Id), cancellationToken: ct); if (validRoles.Count != r.RoleIds.Distinct().Count()) throw new DomainException("One or more roles are invalid.");
        Employee? employee = null;
        if (r.EmployeeId.HasValue) { employee = await employees.GetByIdAsync(r.EmployeeId.Value, ct) ?? throw new KeyNotFoundException("Employee not found."); if (employee.UserId.HasValue) throw new DomainException("Employee already has a login account."); if (!string.Equals(employee.WorkEmail, email, StringComparison.OrdinalIgnoreCase)) throw new DomainException("The login email must match the employee work email."); }
        var user = new UserAccount { TenantId = TenantId, DisplayName = r.DisplayName.Trim(), Email = email, PasswordHash = passwordHasher.Hash(r.Password), IsActive = true };
        await users.AddAsync(user, ct); foreach (var roleId in r.RoleIds.Distinct()) await userRoles.AddAsync(new UserRole { TenantId = TenantId, UserId = user.Id, RoleId = roleId }, ct);
        if (employee is not null) employee.UserId = user.Id;
        await unitOfWork.SaveChangesAsync(ct); return Map(user, r.RoleIds, employee?.Id);
    }
    public async Task<UserAdminDto> ProvisionEmployeeAsync(Guid employeeId, ProvisionEmployeeAccountRequest r, CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(employeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        return await CreateUserAsync(new CreateUserRequest(employee.FullName, employee.WorkEmail, r.Password, r.RoleIds, employee.Id), ct);
    }
    public async Task<UserAdminDto> SetRolesAsync(Guid userId, SetUserRolesRequest r, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException("User not found."); CheckVersion(user, r.Version);
        var validRoles = await roles.ListAsync(x => r.RoleIds.Contains(x.Id), cancellationToken: ct); if (validRoles.Count != r.RoleIds.Distinct().Count()) throw new DomainException("One or more roles are invalid.");
        ValidateDelegation(validRoles.SelectMany(x => x.PermissionsCsv.Split(',')));
        ValidateDelegation(validRoles.SelectMany(x => x.PermissionsCsv.Split(',')));
        var old = await userRoles.ListAsync(x => x.UserId == userId, cancellationToken: ct);
        var oldRoleIds = old.Select(x => x.RoleId).ToArray();
        var oldRoles = await roles.ListAsync(x => oldRoleIds.Contains(x.Id), cancellationToken: ct);
        ValidateDelegation(oldRoles.SelectMany(x => x.PermissionsCsv.Split(',')));
        if (actor.UserId == userId && !validRoles.Any(x => x.PermissionsCsv.Split(',').Any(p => p is Permissions.All or Permissions.IdentityManage)))
            throw new DomainException("You cannot remove your own access administration permission.");
        foreach (var link in old.Where(x => !r.RoleIds.Contains(x.RoleId))) userRoles.Remove(link);
        foreach (var roleId in r.RoleIds.Distinct().Where(id => !old.Any(x => x.RoleId == id))) await userRoles.AddAsync(new UserRole { TenantId = TenantId, UserId = userId, RoleId = roleId }, ct);
        user.UpdatedAt = DateTimeOffset.UtcNow; await notifications.QueueForUsersAsync([userId], "Access roles updated", "Your account roles and permissions were updated.", "security", "/my", ct); await unitOfWork.SaveChangesAsync(ct); return Map(user, r.RoleIds);
    }
    public async Task<UserAdminDto> ResetPasswordAsync(Guid userId, ResetUserPasswordRequest r, CancellationToken ct)
    {
        if (r.Password.Length < 8) throw new DomainException("Password must be at least 8 characters.");
        var user = await users.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException("User not found.");
        var targetRoleIds = (await userRoles.ListAsync(x => x.UserId == userId, cancellationToken: ct)).Select(x => x.RoleId).ToArray();
        ValidateDelegation((await roles.ListAsync(x => targetRoleIds.Contains(x.Id), cancellationToken: ct)).SelectMany(x => x.PermissionsCsv.Split(',')));
        user.PasswordHash = passwordHasher.Hash(r.Password);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var token in await refreshTokens.ListAsync(x => x.UserId == userId && x.RevokedAt == null, cancellationToken: ct)) token.RevokedAt = DateTimeOffset.UtcNow;
        await notifications.QueueForUsersAsync([userId], "Password reset", "Your account password was reset by an administrator. Other sessions were signed out.", "security", "/my", ct);
        await unitOfWork.SaveChangesAsync(ct);
        var roleIds = (await userRoles.ListAsync(x => x.UserId == userId, cancellationToken: ct)).Select(x => x.RoleId).ToArray();
        return Map(user, roleIds, (await employees.FirstOrDefaultAsync(x => x.UserId == user.Id, ct))?.Id);
    }
    public async Task<UserAdminDto> SetActiveAsync(Guid userId, SetUserActiveRequest r, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException("User not found.");
        CheckVersion(user, r.Version);
        if (actor.UserId == userId && !r.IsActive) throw new DomainException("You cannot deactivate your own account.");
        var targetRoleIds = (await userRoles.ListAsync(x => x.UserId == userId, cancellationToken: ct)).Select(x => x.RoleId).ToArray();
        ValidateDelegation((await roles.ListAsync(x => targetRoleIds.Contains(x.Id), cancellationToken: ct)).SelectMany(x => x.PermissionsCsv.Split(',')));
        var linkedEmployee = await employees.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (r.IsActive && linkedEmployee?.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned)
            throw new DomainException("Reactivate the linked employee record before activating this login account.");
        user.IsActive = r.IsActive; user.FailedLoginCount = 0; user.LockedUntil = null; user.UpdatedAt = DateTimeOffset.UtcNow;
        if (!r.IsActive)
            foreach (var token in await refreshTokens.ListAsync(x => x.UserId == userId && x.RevokedAt == null, cancellationToken: ct)) token.RevokedAt = DateTimeOffset.UtcNow;
        await notifications.QueueForUsersAsync([userId], r.IsActive ? "Account activated" : "Account deactivated", r.IsActive ? "Your account was activated by an administrator." : "Your account was deactivated by an administrator.", "security", "/my", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(user, targetRoleIds, linkedEmployee?.Id);
    }
    public async Task<PagedResult<UserAdminDto>> SearchUsersAsync(PagedRequest r, CancellationToken ct)
    {
        var q = r.Search?.Trim().ToLowerInvariant(); System.Linq.Expressions.Expression<Func<UserAccount, bool>> p = x => string.IsNullOrEmpty(q) || x.Email.ToLower().Contains(q) || x.DisplayName.ToLower().Contains(q);
        var total = await users.CountAsync(p, ct); var rows = await users.ListAsync(p, x => x.OrderBy(u => u.DisplayName), r.Skip, r.SafePageSize, ct); var result = new List<UserAdminDto>();
        foreach (var user in rows) result.Add(Map(user, (await userRoles.ListAsync(x => x.UserId == user.Id, cancellationToken: ct)).Select(x => x.RoleId).ToArray(), (await employees.FirstOrDefaultAsync(x => x.UserId == user.Id, ct))?.Id));
        return new(result, r.SafePage, r.SafePageSize, total);
    }
    private void ValidateDelegation(IEnumerable<string> permissions)
    {
        if (permissions.Any(p => !actor.HasPermission(p) && p is not (Permissions.SelfService or Permissions.TeamRead or Permissions.TeamApprove)))
            throw new UnauthorizedAccessException("You cannot grant or administer permissions above your own access level.");
    }
    private static RoleDto Map(Role x) => new(x.Id, x.Name, x.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), x.IsSystem);
    private static UserAdminDto Map(UserAccount x, IEnumerable<Guid> roleIds, Guid? employeeId = null) => new(x.Id, employeeId, x.DisplayName, x.Email, x.IsActive, roleIds.ToArray(), x.Version);
}

public sealed class EmployeeService(IRepository<Employee> employees, IRepository<UserAccount> users, IRepository<RefreshToken> refreshTokens, IRepository<TenantSubscription> subscriptions, IRepository<AuditLog> auditLogs, ICurrentTenant tenant, ICurrentUser actor, IUnitOfWork unitOfWork, IRepository<Department> departments, IRepository<Designation> designations, IRepository<Location> locations) : ServiceBase(tenant), IEmployeeService
{
    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest r, CancellationToken ct)
    {
        Required(r.EmployeeNumber, "Employee number"); Required(r.FirstName, "First name"); Required(r.WorkEmail, "Work email");
        await ValidateEmployee(null, r.FirstName, r.WorkEmail, r.BaseSalary, r.SalaryCurrency, r.EmploymentType, r.DepartmentId, r.DesignationId, r.LocationId, r.ManagerId, ct);
        var employeeNumber = r.EmployeeNumber.Trim().ToLowerInvariant(); var email = r.WorkEmail.Trim().ToLowerInvariant();
        if (await employees.AnyAsync(x => x.EmployeeNumber.ToLower() == employeeNumber || x.WorkEmail == email, ct))
            throw new DomainException("Employee number or work email already exists.");
        var sub = await subscriptions.FirstOrDefaultAsync(x => x.IsActive, ct);
        if (sub is not null && await employees.CountAsync(x => x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod, ct) >= sub.EmployeeLimit)
            throw new DomainException("The subscription employee limit has been reached.");
        var e = new Employee
        {
            TenantId = TenantId, EmployeeNumber = r.EmployeeNumber.Trim(), FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(),
            WorkEmail = r.WorkEmail.Trim().ToLowerInvariant(), Phone = r.Phone, HireDate = r.HireDate, EmploymentType = r.EmploymentType,
            DepartmentId = r.DepartmentId, DesignationId = r.DesignationId, LocationId = r.LocationId, ManagerId = r.ManagerId,
            BaseSalary = r.BaseSalary, SalaryCurrency = r.SalaryCurrency.ToUpperInvariant()
        };
        await employees.AddAsync(e, ct); await unitOfWork.SaveChangesAsync(ct); return Map(e);
    }
    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct) => Map(await employees.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Employee not found."));
    public async Task<PagedResult<EmployeeDto>> SearchAsync(PagedRequest r, EmploymentStatus? status, Guid? departmentId, CancellationToken ct)
    {
        var q = r.Search?.Trim().ToLowerInvariant();
        System.Linq.Expressions.Expression<Func<Employee, bool>> predicate = x =>
            (!status.HasValue || x.Status == status) && (!departmentId.HasValue || x.DepartmentId == departmentId) &&
            (string.IsNullOrEmpty(q) || x.EmployeeNumber.ToLower().Contains(q) || x.FirstName.ToLower().Contains(q) || x.LastName.ToLower().Contains(q) || x.WorkEmail.ToLower().Contains(q));
        var total = await employees.CountAsync(predicate, ct);
        var rows = await employees.ListAsync(predicate, x => x.OrderBy(e => e.EmployeeNumber), r.Skip, r.SafePageSize, ct);
        return new(rows.Select(Map).ToArray(), r.SafePage, r.SafePageSize, total);
    }
    public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeRequest r, CancellationToken ct)
    {
        var e = await employees.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Employee not found."); CheckVersion(e, r.Version);
        Required(r.EmployeeNumber, "Employee number");
        await ValidateEmployee(id, r.FirstName, r.WorkEmail, r.BaseSalary, r.SalaryCurrency, r.EmploymentType, r.DepartmentId, r.DesignationId, r.LocationId, r.ManagerId, ct);
        if (!Enum.IsDefined(r.Status)) throw new DomainException("Employment status is invalid.");
        if (actor.EmployeeId == id && r.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned)
            throw new DomainException("You cannot deactivate your own employee record. Ask another administrator to perform this action.");
        var employeeNumber = r.EmployeeNumber.Trim().ToLowerInvariant();
        var email = r.WorkEmail.Trim().ToLowerInvariant();
        if (await employees.AnyAsync(x => x.Id != id && (x.EmployeeNumber.ToLower() == employeeNumber || x.WorkEmail == email), ct)
            || await users.AnyAsync(x => x.Email == email && x.Id != e.UserId, ct)) throw new DomainException("Employee number or work email already exists.");
        if (r.Status is EmploymentStatus.Terminated or EmploymentStatus.Resigned) e.TerminationDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
        else if (r.Status is EmploymentStatus.Active or EmploymentStatus.Probation or EmploymentStatus.NoticePeriod or EmploymentStatus.Inactive) e.TerminationDate = null;
        e.EmployeeNumber = r.EmployeeNumber.Trim(); e.FirstName = r.FirstName.Trim(); e.LastName = r.LastName.Trim(); e.WorkEmail = r.WorkEmail.Trim().ToLowerInvariant(); e.Phone = r.Phone; e.HireDate = r.HireDate;
        e.Status = r.Status; e.EmploymentType = r.EmploymentType; e.DepartmentId = r.DepartmentId; e.DesignationId = r.DesignationId;
        e.LocationId = r.LocationId; e.ManagerId = r.ManagerId; e.BaseSalary = r.BaseSalary; e.SalaryCurrency = r.SalaryCurrency.ToUpperInvariant();
        if (e.UserId.HasValue && await users.GetByIdAsync(e.UserId.Value, ct) is { } account) { account.Email = e.WorkEmail; account.DisplayName = e.FullName; account.IsActive = r.Status is not (EmploymentStatus.Inactive or EmploymentStatus.Terminated or EmploymentStatus.Resigned or EmploymentStatus.Suspended); if (!account.IsActive) foreach (var token in await refreshTokens.ListAsync(x => x.UserId == account.Id && x.RevokedAt == null, cancellationToken: ct)) token.RevokedAt = DateTimeOffset.UtcNow; }
        await unitOfWork.SaveChangesAsync(ct); return Map(e);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct) { var e = await employees.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Employee not found."); if (e.UserId.HasValue && await users.GetByIdAsync(e.UserId.Value, ct) is { } account) { account.IsActive = false; foreach (var token in await refreshTokens.ListAsync(x => x.UserId == account.Id && x.RevokedAt == null, cancellationToken: ct)) token.RevokedAt = DateTimeOffset.UtcNow; } employees.Remove(e); await unitOfWork.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<LoginHistoryDto>> GetLoginHistoryAsync(Guid id, int take, CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Employee not found.");
        if (!employee.UserId.HasValue) return [];
        var userId = employee.UserId.Value.ToString();
        var rows = await auditLogs.ListAsync(x => x.EntityType == nameof(UserAccount) && x.EntityId == userId && x.Action == "Login", q => q.OrderByDescending(x => x.CreatedAt), take: Math.Clamp(take, 1, 200), cancellationToken: ct);
        return rows.Select(x => new LoginHistoryDto(x.CreatedAt, x.IpAddress, UserAgent(x.AfterJson))).ToArray();
    }
    private async Task ValidateEmployee(Guid? id, string firstName, string email, decimal salary, string currency, EmploymentType employmentType,
        Guid? departmentId, Guid? designationId, Guid? locationId, Guid? managerId, CancellationToken ct)
    {
        Required(firstName, "First name"); Required(email, "Work email"); Required(currency, "Salary currency");
        if (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var address) || address.Address != email.Trim()) throw new DomainException("Work email is invalid.");
        if (salary < 0) throw new DomainException("Base salary cannot be negative.");
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter)) throw new DomainException("Use a three-letter currency code.");
        if (!Enum.IsDefined(employmentType)) throw new DomainException("Employment type is invalid.");
        if (departmentId.HasValue && !await departments.AnyAsync(x => x.Id == departmentId && x.IsActive, ct)) throw new DomainException("Select an active department in this company.");
        if (designationId.HasValue && !await designations.AnyAsync(x => x.Id == designationId && x.IsActive, ct)) throw new DomainException("Select an active designation in this company.");
        if (locationId.HasValue && !await locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct)) throw new DomainException("Select an active location in this company.");
        var visited = new HashSet<Guid>();
        if (id.HasValue) visited.Add(id.Value);
        while (managerId.HasValue)
        {
            if (!visited.Add(managerId.Value)) throw new DomainException("Reporting lines cannot contain a cycle or self-reporting relationship.");
            var manager = await employees.GetByIdAsync(managerId.Value, ct) ?? throw new DomainException("Select a manager in this company.");
            if (manager.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned) throw new DomainException("The reporting manager must be active.");
            managerId = manager.ManagerId;
        }
    }
    private static string? UserAgent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { using var value = System.Text.Json.JsonDocument.Parse(json); return value.RootElement.TryGetProperty("UserAgent", out var agent) ? agent.GetString() : null; }
        catch (System.Text.Json.JsonException) { return null; }
    }
    private static EmployeeDto Map(Employee e) => new(e.Id, e.EmployeeNumber, e.FirstName, e.LastName, e.FullName, e.WorkEmail, e.Phone, e.HireDate, e.Status, e.EmploymentType, e.DepartmentId, e.DesignationId, e.LocationId, e.ManagerId, e.BaseSalary, e.SalaryCurrency, e.UserId, e.Version);
}

public sealed class OrganizationService(IRepository<Department> departments, IRepository<Designation> designations, IRepository<Location> locations, ICurrentTenant tenant, IUnitOfWork unitOfWork) : ServiceBase(tenant), IOrganizationService
{
    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest r, CancellationToken ct) { if (await departments.AnyAsync(x => x.Code == r.Code.ToUpper(), ct)) throw new DomainException("Department code already exists."); var x = new Department { TenantId = TenantId, Name = r.Name.Trim(), Code = r.Code.Trim().ToUpperInvariant(), ParentDepartmentId = r.ParentDepartmentId }; await departments.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct) => (await departments.ListAsync(orderBy: q => q.OrderBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<DesignationDto> CreateDesignationAsync(CreateDesignationRequest r, CancellationToken ct) { if (await designations.AnyAsync(x => x.Code == r.Code.ToUpper(), ct)) throw new DomainException("Designation code already exists."); var x = new Designation { TenantId = TenantId, Name = r.Name.Trim(), Code = r.Code.Trim().ToUpperInvariant(), Level = r.Level, Description = r.Description }; await designations.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<DesignationDto>> ListDesignationsAsync(CancellationToken ct) => (await designations.ListAsync(orderBy: q => q.OrderBy(x => x.Level).ThenBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<LocationDto> CreateLocationAsync(CreateLocationRequest r, CancellationToken ct) { if (await locations.AnyAsync(x => x.Code == r.Code.ToUpper(), ct)) throw new DomainException("Location code already exists."); var x = new Location { TenantId = TenantId, Name = r.Name.Trim(), Code = r.Code.Trim().ToUpperInvariant(), Address = r.Address, City = r.City, CountryCode = r.CountryCode?.ToUpperInvariant() }; await locations.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<LocationDto>> ListLocationsAsync(CancellationToken ct) => (await locations.ListAsync(orderBy: q => q.OrderBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    private static DepartmentDto Map(Department x) => new(x.Id, x.Name, x.Code, x.ParentDepartmentId, x.HeadEmployeeId, x.IsActive);
    private static DesignationDto Map(Designation x) => new(x.Id, x.Name, x.Code, x.Level, x.Description, x.IsActive);
    private static LocationDto Map(Location x) => new(x.Id, x.Name, x.Code, x.Address, x.City, x.CountryCode, x.IsActive);
}

public sealed class LeaveService(IRepository<LeaveType> types, IRepository<LeaveBalance> balances, IRepository<LeaveRequest> requests, IRepository<Employee> employees, IRepository<StoredDocument> documents, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork unitOfWork, INotificationService notifications, IRepository<AttendancePolicy> policies, IRepository<Holiday> holidays, IRepository<Tenant> tenants) : ServiceBase(tenant), ILeaveService
{
    public async Task<LeaveTypeDto> CreateTypeAsync(CreateLeaveTypeRequest r, CancellationToken ct) { if (await types.AnyAsync(x => x.Code == r.Code.ToUpper(), ct)) throw new DomainException("Leave type code already exists."); var x = new LeaveType { TenantId = TenantId, Name = r.Name.Trim(), Code = r.Code.Trim().ToUpperInvariant(), AnnualAllowance = r.AnnualAllowance, IsPaid = r.IsPaid, RequiresDocument = r.RequiresDocument, MaxConsecutiveDays = r.MaxConsecutiveDays }; await types.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<LeaveTypeDto>> ListTypesAsync(CancellationToken ct) => (await types.ListAsync(x => x.IsActive, q => q.OrderBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest r, CancellationToken ct)
    {
        if (r.EndsOn < r.StartsOn || r.Days <= 0) throw new DomainException("Leave dates or day count are invalid.");
        if (r.StartsOn.Year != r.EndsOn.Year) throw new DomainException("Submit separate requests for each leave year so balances are charged correctly.");
        Required(r.Reason, "Leave reason");
        if (r.Reason.Trim().Length > 2000) throw new DomainException("Leave reason cannot exceed 2,000 characters.");
        var employee = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned || r.StartsOn < employee.HireDate)
            throw new DomainException("Leave requires an active employee and cannot precede their hire date.");
        var type = await types.GetByIdAsync(r.LeaveTypeId, ct) ?? throw new KeyNotFoundException("Leave type not found.");
        if (!type.IsActive) throw new DomainException("This leave type is inactive.");
        var policy = await policies.FirstOrDefaultAsync(_ => true, ct) ?? new AttendancePolicy();
        var workingDays = policy.WorkingDaysCsv.Split(',').Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedDates = (await holidays.ListAsync(x => !x.IsOptional && (!x.LocationId.HasValue || x.LocationId == employee.LocationId) && x.Date >= r.StartsOn && x.Date <= r.EndsOn, cancellationToken: ct)).Select(x => x.Date).ToHashSet();
        var scheduledDays = Enumerable.Range(0, r.EndsOn.DayNumber - r.StartsOn.DayNumber + 1).Select(r.StartsOn.AddDays)
            .Count(date => workingDays.Contains(date.DayOfWeek.ToString()) && !excludedDates.Contains(date));
        if (scheduledDays == 0 || (r.Days != scheduledDays && !(r.StartsOn == r.EndsOn && scheduledDays == 1 && r.Days == .5m)))
            throw new DomainException($"This date range contains {scheduledDays} scheduled working day(s). Request that count, or 0.5 for a single half-day.");
        if (type.MaxConsecutiveDays > 0 && r.Days > type.MaxConsecutiveDays) throw new DomainException("Requested leave exceeds the maximum consecutive days.");
        if (await requests.AnyAsync(x => x.EmployeeId == r.EmployeeId && x.Status != LeaveRequestStatus.Rejected && x.Status != LeaveRequestStatus.Cancelled && x.StartsOn <= r.EndsOn && x.EndsOn >= r.StartsOn, ct)) throw new DomainException("The leave request overlaps an existing request.");
        var balance = await GetOrCreateBalance(r.EmployeeId, type, r.StartsOn.Year, ct);
        if (balance.Available < r.Days) throw new DomainException("Insufficient leave balance.");
        var x = new LeaveRequest { TenantId = TenantId, EmployeeId = r.EmployeeId, LeaveTypeId = r.LeaveTypeId, StartsOn = r.StartsOn, EndsOn = r.EndsOn, Days = r.Days, Reason = r.Reason.Trim() };
        balance.Pending += r.Days; await requests.AddAsync(x, ct);
        await notifications.QueueForPermissionAsync(Permissions.LeaveManage, "Leave request submitted",
            $"{employee.FullName} requested {r.Days:0.##} day(s) of {type.Name} from {r.StartsOn:dd MMM yyyy} to {r.EndsOn:dd MMM yyyy}.",
            "leave", "/leave", ct);
        await unitOfWork.SaveChangesAsync(ct); return Map(x);
    }
    public async Task<LeaveRequestDto> ReviewAsync(Guid id, ReviewLeaveRequest r, CancellationToken ct)
    {
        var x = await requests.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Leave request not found."); CheckVersion(x, r.Version);
        if (x.Status != LeaveRequestStatus.Pending) throw new DomainException("Only pending requests can be reviewed.");
        if (user.EmployeeId == x.EmployeeId) throw new UnauthorizedAccessException("You cannot review your own leave request.");
        if (!r.Approve && string.IsNullOrWhiteSpace(r.Comment)) throw new DomainException("A rejection reason is required.");
        if (r.Comment?.Trim().Length > 2000) throw new DomainException("Review comment cannot exceed 2,000 characters.");
        var type = await types.GetByIdAsync(x.LeaveTypeId, ct) ?? throw new KeyNotFoundException("Leave type not found.");
        if (r.Approve && type.RequiresDocument && !await documents.AnyAsync(d => d.OwnerType == DocumentOwnerType.LeaveRequest && d.OwnerId == x.Id && d.Category == "supporting-document", ct))
            throw new DomainException($"A supporting document is required before {type.Name} can be approved.");
        var balance = await GetOrCreateBalance(x.EmployeeId, type, x.StartsOn.Year, ct); balance.Pending = Math.Max(0, balance.Pending - x.Days);
        if (r.Approve) { x.Status = LeaveRequestStatus.Approved; balance.Used += x.Days; } else x.Status = LeaveRequestStatus.Rejected;
        x.ReviewedBy = user.UserId; x.ReviewedAt = DateTimeOffset.UtcNow; x.ReviewComment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim();
        await notifications.QueueForEmployeesAsync([x.EmployeeId], $"Leave request {x.Status.ToString().ToLowerInvariant()}",
            $"Your {type.Name} leave request for {x.StartsOn:dd MMM yyyy} to {x.EndsOn:dd MMM yyyy} was {x.Status.ToString().ToLowerInvariant()}.",
            "leave", "/my-services", ct);
        await unitOfWork.SaveChangesAsync(ct); return Map(x);
    }
    public async Task<PagedResult<LeaveRequestDto>> SearchAsync(PagedRequest r, Guid? employeeId, LeaveRequestStatus? status, CancellationToken ct)
    {
        var query = r.Search?.Trim().ToLowerInvariant();
        System.Linq.Expressions.Expression<Func<LeaveRequest, bool>> p = x => (!employeeId.HasValue || x.EmployeeId == employeeId) && (!status.HasValue || x.Status == status)
            && (string.IsNullOrEmpty(query) || x.Reason.ToLower().Contains(query));
        var total = await requests.CountAsync(p, ct);
        var rows = await requests.ListAsync(p, q => q.OrderBy(x => x.Status == LeaveRequestStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatedAt), r.Skip, r.SafePageSize, ct);
        return new(rows.Select(Map).ToArray(), r.SafePage, r.SafePageSize, total);
    }
    public async Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(Guid employeeId, int year, CancellationToken ct)
    {
        _ = await employees.GetByIdAsync(employeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        if (year is < 2000 or > 2200) throw new DomainException("Leave year is invalid.");
        var allTypes = await types.ListAsync(x => x.IsActive, cancellationToken: ct); var result = new List<LeaveBalanceDto>();
        foreach (var type in allTypes) { var x = await GetOrCreateBalance(employeeId, type, year, ct); result.Add(Map(x)); }
        await unitOfWork.SaveChangesAsync(ct); return result;
    }
    private async Task<LeaveBalance> GetOrCreateBalance(Guid employeeId, LeaveType type, int year, CancellationToken ct) { var x = await balances.FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == type.Id && b.Year == year, ct); if (x is not null) return x; x = new LeaveBalance { TenantId = TenantId, EmployeeId = employeeId, LeaveTypeId = type.Id, Year = year, Entitled = type.AnnualAllowance }; await balances.AddAsync(x, ct); return x; }
    public async Task<LeaveRequestDto> CancelAsync(Guid id, long version, CancellationToken ct)
    {
        var row = await requests.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Leave request not found.");
        if (!user.HasPermission(Permissions.LeaveManage) && !(user.HasPermission(Permissions.SelfService) && user.EmployeeId == row.EmployeeId))
            throw new UnauthorizedAccessException("You can only cancel your own leave.");
        CheckVersion(row, version);
        if (row.Status is not (LeaveRequestStatus.Pending or LeaveRequestStatus.Approved)) throw new DomainException("Only pending or approved requests can be cancelled.");
        var company = await tenants.GetByIdAsync(TenantId, ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, AttendanceCalendar.Zone(company?.TimeZone)).DateTime);
        if (row.Status == LeaveRequestStatus.Approved && row.StartsOn <= today) throw new DomainException("Approved leave that has started requires an HR adjustment.");
        var type = await types.GetByIdAsync(row.LeaveTypeId, ct) ?? throw new KeyNotFoundException("Leave type not found.");
        var balance = await GetOrCreateBalance(row.EmployeeId, type, row.StartsOn.Year, ct);
        if (row.Status == LeaveRequestStatus.Pending) balance.Pending = Math.Max(0, balance.Pending - row.Days);
        else balance.Used = Math.Max(0, balance.Used - row.Days);
        row.Status = LeaveRequestStatus.Cancelled;
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row);
    }
    private static LeaveTypeDto Map(LeaveType x) => new(x.Id, x.Name, x.Code, x.AnnualAllowance, x.IsPaid, x.RequiresDocument, x.MaxConsecutiveDays);
    private static LeaveRequestDto Map(LeaveRequest x) => new(x.Id, x.EmployeeId, x.LeaveTypeId, x.StartsOn, x.EndsOn, x.Days, x.Reason, x.Status, x.ReviewComment, x.CreatedAt, x.ReviewedAt, x.Version);
    private static LeaveBalanceDto Map(LeaveBalance x) => new(x.LeaveTypeId, x.Year, x.Entitled, x.Used, x.Pending, x.Available);
}

public sealed class AttendanceService(IRepository<AttendanceRecord> records, IRepository<AttendancePolicy> policies, IRepository<Employee> employees, IRepository<Tenant> tenants, IRepository<Holiday> holidays, IRepository<LeaveRequest> leaveRequests, ICurrentTenant tenant, IUnitOfWork unitOfWork) : ServiceBase(tenant), IAttendanceService
{
    public async Task<AttendanceDto> ClockInAsync(ClockRequest r, CancellationToken ct)
    {
        ValidateCoordinates(r);
        var employee = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status is EmploymentStatus.Inactive or EmploymentStatus.Suspended or EmploymentStatus.Terminated or EmploymentStatus.Resigned) throw new DomainException("Attendance cannot be recorded for an inactive employee.");
        var at = r.Timestamp?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        if (at > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("Check-in time cannot be in the future.");
        var policy = await Policy(ct);
        var company = await tenants.GetByIdAsync(TenantId, ct);
        var date = AttendanceCalendar.WorkDate(at, AttendanceCalendar.Zone(company?.TimeZone), policy.OfficeStartsAt, policy.OfficeEndsAt);
        if (date < employee.HireDate) throw new DomainException("Attendance cannot precede the employee's hire date.");
        if (await records.AnyAsync(x => x.EmployeeId == r.EmployeeId && x.ClockedOutAt == null, ct)) throw new DomainException("Employee already has an open attendance session.");
        if (await records.AnyAsync(x => x.EmployeeId == r.EmployeeId && x.ClockedOutAt > at, ct)) throw new DomainException("Check-in cannot overlap or precede an existing attendance session.");
        var source = NormalizeSource(r.Source);
        var notes = NormalizeNotes(r.Notes);
        if (source == "admin" && notes is null) throw new DomainException("A reason is required for a manual attendance entry.");
        var x = new AttendanceRecord { TenantId = TenantId, EmployeeId = r.EmployeeId, WorkDate = date, ClockedInAt = at, Source = source, Notes = notes, ScheduledStartAt = policy.OfficeStartsAt, ScheduledEndAt = policy.OfficeEndsAt, RequiredMinutes = policy.RequiredMinutesPerDay, LateGraceMinutes = policy.LateGraceMinutes, EarlyDepartureGraceMinutes = policy.EarlyDepartureGraceMinutes, ClockInLatitude = r.Latitude, ClockInLongitude = r.Longitude, ClockInAccuracyMeters = r.AccuracyMeters, ClockInAddress = r.Address, ClockInIpAddress = r.IpAddress, ClockInUserAgent = r.UserAgent };
        await records.AddAsync(x, ct);
        employee.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        return Map(x);
    }
    public async Task<AttendanceDto> ClockOutAsync(ClockRequest r, CancellationToken ct)
    {
        ValidateCoordinates(r);
        var at = r.Timestamp?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        if (at > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("Check-out time cannot be in the future.");
        var open = await records.ListAsync(x => x.EmployeeId == r.EmployeeId && x.ClockedOutAt == null, q => q.OrderByDescending(x => x.ClockedInAt), take: 1, cancellationToken: ct);
        var x = open.FirstOrDefault() ?? throw new DomainException("No open clock-in exists for this employee.");
        if (NormalizeSource(r.Source) == "admin" && string.IsNullOrWhiteSpace(r.Notes)) throw new DomainException("A reason is required for a manual attendance entry.");
        AttendanceCalendar.ValidateInterval(x.ClockedInAt!.Value, at);
        if (await records.AnyAsync(other => other.EmployeeId == x.EmployeeId && other.Id != x.Id && other.ClockedInAt < at && (other.ClockedOutAt == null || other.ClockedOutAt > x.ClockedInAt), ct))
            throw new DomainException("Attendance sessions cannot overlap.");
        x.ClockedOutAt = at;
        x.WorkHours = Math.Round((decimal)(at - x.ClockedInAt!.Value).TotalHours, 2);
        var earlierHours = (await records.ListAsync(session => session.EmployeeId == x.EmployeeId && session.WorkDate == x.WorkDate && session.Id != x.Id, cancellationToken: ct)).Sum(session => session.WorkHours);
        var requiredHours = (x.RequiredMinutes ?? (await Policy(ct)).RequiredMinutesPerDay) / 60m;
        x.OvertimeHours = Math.Round(Math.Max(0, earlierHours + x.WorkHours - requiredHours) - Math.Max(0, earlierHours - requiredHours), 2);
        x.ClockOutLatitude = r.Latitude; x.ClockOutLongitude = r.Longitude; x.ClockOutAccuracyMeters = r.AccuracyMeters;
        x.ClockOutAddress = r.Address; x.ClockOutIpAddress = r.IpAddress; x.ClockOutUserAgent = r.UserAgent;
        if (!string.IsNullOrWhiteSpace(r.Notes))
        {
            var checkoutNote = NormalizeNotes(r.Notes);
            x.Notes = string.IsNullOrWhiteSpace(x.Notes) ? checkoutNote : NormalizeNotes($"{x.Notes}\nCheck-out: {checkoutNote}");
        }
        await unitOfWork.SaveChangesAsync(ct);
        return Map(x);
    }
    public async Task<PagedResult<AttendanceDto>> SearchAsync(PagedRequest r, Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken ct) { System.Linq.Expressions.Expression<Func<AttendanceRecord, bool>> p = x => (!employeeId.HasValue || x.EmployeeId == employeeId) && (!from.HasValue || x.WorkDate >= from) && (!to.HasValue || x.WorkDate <= to); var total = await records.CountAsync(p, ct); var rows = await records.ListAsync(p, q => q.OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.ClockedInAt), r.Skip, r.SafePageSize, ct); return new(rows.Select(Map).ToArray(), r.SafePage, r.SafePageSize, total); }
    public async Task<IReadOnlyList<AttendancePolicyDto>> GetPolicyAsync(CancellationToken ct) => [MapPolicy(await Policy(ct))];
    public async Task<AttendancePolicyDto> UpdatePolicyAsync(UpdateAttendancePolicyRequest r, CancellationToken ct)
    {
        var requiredMinutes = AttendanceCalendar.DurationMinutes(r.OfficeStartsAt, r.OfficeEndsAt);
        if (r.LateGraceMinutes is < 0 or > 180 || r.EarlyDepartureGraceMinutes is < 0 or > 180) throw new DomainException("Grace periods must be between 0 and 180 minutes.");
        var days = r.WorkingDays.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (days.Length == 0 || days.Any(x => !Enum.GetNames<DayOfWeek>().Contains(x, StringComparer.OrdinalIgnoreCase))) throw new DomainException("Working days are invalid.");
        var policy = await policies.FirstOrDefaultAsync(_ => true, ct);
        if (policy is null) { policy = new AttendancePolicy { TenantId = TenantId }; await policies.AddAsync(policy, ct); }
        else CheckVersion(policy, r.Version);
        policy.OfficeStartsAt = r.OfficeStartsAt; policy.OfficeEndsAt = r.OfficeEndsAt;
        policy.RequiredMinutesPerDay = requiredMinutes; policy.LateGraceMinutes = r.LateGraceMinutes;
        policy.EarlyDepartureGraceMinutes = r.EarlyDepartureGraceMinutes; policy.WorkingDaysCsv = string.Join(',', days); policy.RequireLocationCapture = r.RequireLocationCapture;
        await unitOfWork.SaveChangesAsync(ct); return MapPolicy(policy);
    }
    public async Task<PagedResult<DailyAttendanceReportDto>> ReportAsync(PagedRequest r, Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, end, today) = await ResolveRange(from, to, ct);
        var rows = await ReportRows(employeeId, start, end, today, ct);
        return new(rows.Skip(r.Skip).Take(r.SafePageSize).ToArray(), r.SafePage, r.SafePageSize, rows.Count);
    }
    public async Task<IReadOnlyList<AttendanceSummaryDto>> SummaryAsync(Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, end, today) = await ResolveRange(from, to, ct);
        var rows = await ReportRows(employeeId, start, end, today, ct);
        return rows.GroupBy(x => new { x.EmployeeId, x.EmployeeName }).Select(group =>
        {
            var worked = group.Where(x => x.SessionCount > 0).ToArray();
            return new AttendanceSummaryDto(group.Key.EmployeeId, group.Key.EmployeeName, start, end,
                group.Count(x => x.RequiredHours > 0), worked.Length, group.Count(x => x.Status == "Absent"), group.Count(x => x.Status == "On approved leave"),
                Math.Round(worked.Sum(x => x.TotalHours), 2), worked.Length == 0 ? 0 : Math.Round(worked.Average(x => x.TotalHours), 2),
                worked.Count(x => x.LateMinutes > 0), worked.Count(x => x.EarlyDepartureMinutes > 0), worked.Count(x => x.ShortfallHours > 0), Math.Round(worked.Sum(x => x.OvertimeHours), 2));
        }).OrderBy(x => x.EmployeeName).ToArray();
    }
    private static void ValidateCoordinates(ClockRequest r) { if (r.Latitude is < -90 or > 90 || r.Longitude is < -180 or > 180 || r.AccuracyMeters < 0) throw new DomainException("Location coordinates are invalid."); }
    internal static AttendanceDto Map(AttendanceRecord x) => new(x.Id, x.EmployeeId, x.WorkDate, x.ClockedInAt, x.ClockedOutAt, x.Status, x.ClockedOutAt.HasValue ? "Completed" : "In progress", x.WorkHours, x.OvertimeHours, x.Source, x.Notes, x.ClockInLatitude, x.ClockInLongitude, x.ClockInAccuracyMeters, x.ClockInAddress, x.ClockInIpAddress, x.ClockInUserAgent, x.ClockOutLatitude, x.ClockOutLongitude, x.ClockOutAccuracyMeters, x.ClockOutAddress, x.ClockOutIpAddress, x.ClockOutUserAgent, x.Version);
    private static AttendancePolicyDto MapPolicy(AttendancePolicy x) => new(x.Id == Guid.Empty ? null : x.Id, x.OfficeStartsAt, x.OfficeEndsAt, $"{x.RequiredMinutesPerDay / 60} h {x.RequiredMinutesPerDay % 60} min", x.LateGraceMinutes, x.EarlyDepartureGraceMinutes, x.WorkingDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), x.RequireLocationCapture, x.Version);
    private async Task<AttendancePolicy> Policy(CancellationToken ct) => await policies.FirstOrDefaultAsync(_ => true, ct) ?? new AttendancePolicy { TenantId = TenantId };
    private async Task<List<DailyAttendanceReportDto>> ReportRows(Guid? employeeId, DateOnly start, DateOnly end, DateOnly today, CancellationToken ct)
    {
        var policy = await Policy(ct);
        var company = await tenants.GetByIdAsync(TenantId, ct);
        TimeZoneInfo zone; try { zone = TimeZoneInfo.FindSystemTimeZoneById(company?.TimeZone ?? "UTC"); } catch { zone = TimeZoneInfo.Utc; }
        var sessions = await records.ListAsync(x => (!employeeId.HasValue || x.EmployeeId == employeeId) && x.WorkDate >= start && x.WorkDate <= end, cancellationToken: ct);
        var people = await employees.ListAsync(x => (!employeeId.HasValue || x.Id == employeeId) && x.HireDate <= end, q => q.OrderBy(x => x.FirstName).ThenBy(x => x.LastName), cancellationToken: ct);
        if (employeeId.HasValue && people.Count == 0) throw new KeyNotFoundException("Employee not found.");
        var companyHolidays = await holidays.ListAsync(x => x.Date >= start && x.Date <= end && !x.IsOptional, cancellationToken: ct);
        var approvedLeave = await leaveRequests.ListAsync(x => x.Status == LeaveRequestStatus.Approved && x.StartsOn <= end && x.EndsOn >= start && (!employeeId.HasValue || x.EmployeeId == employeeId), cancellationToken: ct);
        var workingDays = policy.WorkingDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Enum.TryParse<DayOfWeek>(x, true, out var day) ? day : (DayOfWeek?)null).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        var sessionGroups = sessions.GroupBy(x => new { x.EmployeeId, x.WorkDate }).ToDictionary(x => (x.Key.EmployeeId, x.Key.WorkDate), x => x.ToArray());
        var result = new List<DailyAttendanceReportDto>();
        foreach (var person in people)
        {
            var firstDate = person.HireDate > start ? person.HireDate : start;
            for (var date = firstDate; date <= end && date <= today && (!person.TerminationDate.HasValue || date <= person.TerminationDate.Value); date = date.AddDays(1))
            {
                sessionGroups.TryGetValue((person.Id, date), out var daySessions);
                daySessions ??= [];
                var isWorkingDay = workingDays.Contains(date.DayOfWeek);
                var holiday = companyHolidays.Any(x => x.Date == date && (!x.LocationId.HasValue || x.LocationId == person.LocationId));
                var onLeave = approvedLeave.Any(x => x.EmployeeId == person.Id && x.StartsOn <= date && x.EndsOn >= date);
                if (daySessions.Length == 0)
                {
                    if (!isWorkingDay && !holiday) continue;
                    var status = holiday ? "Company holiday" : onLeave ? "On approved leave" : date == today ? "Not checked in" : "Absent";
                    var scheduledHours = holiday || onLeave ? 0 : Math.Round(policy.RequiredMinutesPerDay / 60m, 2);
                    var shortfallHours = date == today ? 0 : scheduledHours;
                    result.Add(new DailyAttendanceReportDto(person.Id, person.FullName, date, null, null, 0, scheduledHours, 0, 0, 0, shortfallHours, 0, status));
                    continue;
                }
                var first = daySessions.Where(x => x.ClockedInAt.HasValue).MinBy(x => x.ClockedInAt)?.ClockedInAt;
                var last = daySessions.Where(x => x.ClockedOutAt.HasValue).MaxBy(x => x.ClockedOutAt)?.ClockedOutAt;
                var open = daySessions.Where(x => x.ClockedOutAt is null && x.ClockedInAt.HasValue).ToArray();
                var stale = open.Any(x => DateTimeOffset.UtcNow - x.ClockedInAt!.Value > TimeSpan.FromHours(24));
                var liveHours = open.Where(x => DateTimeOffset.UtcNow - x.ClockedInAt!.Value <= TimeSpan.FromHours(24)).Sum(x => Math.Max(0, (decimal)(DateTimeOffset.UtcNow - x.ClockedInAt!.Value).TotalHours));
                var total = Math.Round(daySessions.Sum(x => x.WorkHours) + liveHours, 2);
                var snapshot = daySessions.OrderBy(x => x.ClockedInAt).First();
                var scheduledStart = snapshot.ScheduledStartAt ?? policy.OfficeStartsAt;
                var scheduledEnd = snapshot.ScheduledEndAt ?? policy.OfficeEndsAt;
                var lateGrace = snapshot.LateGraceMinutes ?? policy.LateGraceMinutes;
                var earlyGrace = snapshot.EarlyDepartureGraceMinutes ?? policy.EarlyDepartureGraceMinutes;
                var requiredMinutes = snapshot.RequiredMinutes ?? policy.RequiredMinutesPerDay;
                var firstTime = first.HasValue ? TimeZoneInfo.ConvertTime(first.Value, zone).DateTime : (DateTime?)null;
                var lastTime = last.HasValue ? TimeZoneInfo.ConvertTime(last.Value, zone).DateTime : (DateTime?)null;
                var schedule = AttendanceCalendar.Schedule(date, scheduledStart, scheduledEnd);
                var required = isWorkingDay && !holiday && !onLeave ? Math.Round(requiredMinutes / 60m, 2) : 0;
                var late = required > 0 && firstTime.HasValue ? Math.Max(0, (int)(firstTime.Value - schedule.Start.AddMinutes(lateGrace)).TotalMinutes) : 0;
                var early = required > 0 && lastTime.HasValue && open.Length == 0 ? Math.Max(0, (int)(schedule.End.AddMinutes(-earlyGrace) - lastTime.Value).TotalMinutes) : 0;
                var overtime = Math.Round(Math.Max(0, total - required), 2);
                var shortfall = open.Length > 0 ? 0 : Math.Round(Math.Max(0, required - total), 2);
                var issues = new List<string>();
                if (stale) issues.Add("Missing check-out · correction required");
                else if (open.Length > 0) issues.Add("In progress");
                else if (daySessions.Any(x => x.ClockedInAt.HasValue && x.ClockedOutAt.HasValue && x.ClockedOutAt.Value - x.ClockedInAt.Value > TimeSpan.FromHours(24))) issues.Add("Review required");
                if (holiday) issues.Add("Worked on holiday"); else if (onLeave) issues.Add("Worked during approved leave");
                if (late > 0) issues.Add("Late arrival");
                if (early > 0) issues.Add("Early departure");
                if (shortfall > 0) issues.Add("Insufficient hours");
                result.Add(new DailyAttendanceReportDto(person.Id, person.FullName, date, first, last, total, required, late, early, overtime, shortfall, daySessions.Length, issues.Count == 0 ? "Compliant" : string.Join(" · ", issues)));
            }
        }
        return result.OrderByDescending(x => x.WorkDate).ThenBy(x => x.EmployeeName).ToList();
    }
    private async Task<(DateOnly Start, DateOnly End, DateOnly Today)> ResolveRange(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = await LocalDate(DateTimeOffset.UtcNow, ct);
        var end = to ?? today; var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end) throw new DomainException("From date must be on or before to date.");
        if (end.DayNumber - start.DayNumber > 366) throw new DomainException("Attendance reports are limited to a 12-month range.");
        return (start, end, today);
    }
    private static string NormalizeSource(string source)
    {
        var value = source.Trim().ToLowerInvariant();
        return value is "web" or "mobile" or "kiosk" or "admin" ? value : throw new DomainException("Attendance source is invalid.");
    }
    private static string? NormalizeNotes(string? notes)
    {
        var value = notes?.Trim();
        if (value?.Length > 500) throw new DomainException("Attendance notes cannot exceed 500 characters.");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
    private async Task<DateOnly> LocalDate(DateTimeOffset timestamp, CancellationToken ct)
    {
        var company = await tenants.GetByIdAsync(TenantId, ct);
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, TimeZoneInfo.FindSystemTimeZoneById(company?.TimeZone ?? "UTC")).DateTime); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(timestamp.UtcDateTime); }
        catch (InvalidTimeZoneException) { return DateOnly.FromDateTime(timestamp.UtcDateTime); }
    }
}

public sealed class WorkforceOperationsService(IRepository<Shift> shifts, IRepository<Holiday> holidays, IRepository<TimesheetEntry> timesheets, IRepository<EmployeeDocument> documents, IRepository<Announcement> announcements, IRepository<Employee> employees, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), IWorkforceOperationsService
{
    public async Task<ShiftDto> CreateShiftAsync(CreateShiftRequest r, CancellationToken ct) { var x = new Shift { TenantId = TenantId, Name = r.Name.Trim(), StartsAt = r.StartsAt, EndsAt = r.EndsAt, GraceMinutes = Math.Max(0, r.GraceMinutes), IsNightShift = r.IsNightShift }; await shifts.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<ShiftDto>> ListShiftsAsync(CancellationToken ct) => (await shifts.ListAsync(orderBy: q => q.OrderBy(x => x.Name), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest r, CancellationToken ct) { if (await holidays.AnyAsync(x => x.Date == r.Date && x.LocationId == r.LocationId, ct)) throw new DomainException("Holiday already exists for this date and location."); var x = new Holiday { TenantId = TenantId, Name = r.Name.Trim(), Date = r.Date, LocationId = r.LocationId, IsOptional = r.IsOptional }; await holidays.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(int year, CancellationToken ct) => (await holidays.ListAsync(x => x.Date.Year == year, q => q.OrderBy(x => x.Date), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<TimesheetDto> SubmitTimesheetAsync(SubmitTimesheetRequest r, CancellationToken ct) { var employee = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); if (r.Hours <= 0 || r.Hours > 24) throw new DomainException("Timesheet hours must be between 0 and 24."); var dayHours = (await timesheets.ListAsync(x => x.EmployeeId == r.EmployeeId && x.WorkDate == r.WorkDate && x.Status != WorkflowStatus.Rejected, cancellationToken: ct)).Sum(x => x.Hours); if (dayHours + r.Hours > 24) throw new DomainException("Total timesheet hours cannot exceed 24 per day."); var x = new TimesheetEntry { TenantId = TenantId, EmployeeId = r.EmployeeId, WorkDate = r.WorkDate, Description = r.Description.Trim(), Hours = r.Hours, ProjectCode = r.ProjectCode }; await timesheets.AddAsync(x, ct); await notifications.QueueForPermissionAsync(Permissions.WorkforceManage, "Timesheet submitted", $"{employee.FullName} submitted {r.Hours:0.##} hours for {r.WorkDate:dd MMM yyyy}.", "timesheet", "/workforce", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<TimesheetDto> ReviewTimesheetAsync(Guid id, ReviewTimesheetRequest r, CancellationToken ct) { var x = await timesheets.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Timesheet not found."); CheckVersion(x, r.Version); if (x.Status != WorkflowStatus.Pending) throw new DomainException("Only pending timesheets can be reviewed."); x.Status = r.Approve ? WorkflowStatus.Approved : WorkflowStatus.Rejected; await notifications.QueueForEmployeesAsync([x.EmployeeId], $"Timesheet {x.Status.ToString().ToLowerInvariant()}", $"Your timesheet for {x.WorkDate:dd MMM yyyy} was {x.Status.ToString().ToLowerInvariant()}.", "timesheet", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<PagedResult<TimesheetDto>> SearchTimesheetsAsync(PagedRequest r, Guid? employeeId, WorkflowStatus? status, CancellationToken ct) { System.Linq.Expressions.Expression<Func<TimesheetEntry, bool>> p = x => (!employeeId.HasValue || x.EmployeeId == employeeId) && (!status.HasValue || x.Status == status); var total = await timesheets.CountAsync(p, ct); var rows = await timesheets.ListAsync(p, q => q.OrderByDescending(x => x.WorkDate), r.Skip, r.SafePageSize, ct); return new(rows.Select(Map).ToArray(), r.SafePage, r.SafePageSize, total); }
    public async Task<EmployeeDocumentDto> AddDocumentAsync(AddEmployeeDocumentRequest r, CancellationToken ct) { _ = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); var x = new EmployeeDocument { TenantId = TenantId, EmployeeId = r.EmployeeId, DocumentType = r.DocumentType, FileName = r.FileName, StorageKey = r.StorageKey, ContentType = r.ContentType, ExpiresOn = r.ExpiresOn }; await documents.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<EmployeeDocumentDto> VerifyDocumentAsync(Guid id, VerifyDocumentRequest r, CancellationToken ct) { var x = await documents.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Document not found."); CheckVersion(x, r.Version); x.Status = r.Status; await notifications.QueueForEmployeesAsync([x.EmployeeId], $"Document {x.Status.ToString().ToLowerInvariant()}", $"Your {x.DocumentType} document was marked {x.Status.ToString().ToLowerInvariant()}.", "document", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<EmployeeDocumentDto>> ListDocumentsAsync(Guid employeeId, CancellationToken ct) => (await documents.ListAsync(x => x.EmployeeId == employeeId, q => q.OrderBy(x => x.DocumentType), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest r, CancellationToken ct) { var x = new Announcement { TenantId = TenantId, Title = r.Title.Trim(), Body = r.Body, PublishedAt = DateTimeOffset.UtcNow, ExpiresAt = r.ExpiresAt, Audience = r.Audience }; await announcements.AddAsync(x, ct); await notifications.QueueForAllUsersAsync(x.Title, x.Body, "announcement", "/my", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(CancellationToken ct) { var now = DateTimeOffset.UtcNow; return (await announcements.ListAsync(x => x.ExpiresAt == null || x.ExpiresAt > now, q => q.OrderByDescending(x => x.PublishedAt), cancellationToken: ct)).Select(Map).ToArray(); }
    private static ShiftDto Map(Shift x) => new(x.Id, x.Name, x.StartsAt, x.EndsAt, x.GraceMinutes, x.IsNightShift); private static HolidayDto Map(Holiday x) => new(x.Id, x.Name, x.Date, x.LocationId, x.IsOptional); private static TimesheetDto Map(TimesheetEntry x) => new(x.Id, x.EmployeeId, x.WorkDate, x.ProjectCode, x.Description, x.Hours, x.Status, x.Version); private static EmployeeDocumentDto Map(EmployeeDocument x) => new(x.Id, x.EmployeeId, x.DocumentType, x.FileName, x.StorageKey, x.ContentType, x.ExpiresOn, x.Status, x.Version); private static AnnouncementDto Map(Announcement x) => new(x.Id, x.Title, x.Body, x.PublishedAt, x.ExpiresAt, x.Audience);
}

public sealed class PayrollService(IRepository<PayrollRun> runs, IRepository<PayrollItem> items, IRepository<Employee> employees, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), IPayrollService
{
    public async Task<PayrollRunDto> CreateAsync(CreatePayrollRunRequest r, CancellationToken ct) { if (r.PeriodEnd < r.PeriodStart) throw new DomainException("Payroll period is invalid."); if (await runs.AnyAsync(x => x.PeriodStart == r.PeriodStart && x.PeriodEnd == r.PeriodEnd && x.Status != PayrollRunStatus.Cancelled, ct)) throw new DomainException("A payroll run already exists for this period."); var x = new PayrollRun { TenantId = TenantId, Name = r.Name.Trim(), PeriodStart = r.PeriodStart, PeriodEnd = r.PeriodEnd, PaymentDate = r.PaymentDate, Currency = r.Currency.ToUpperInvariant() }; await runs.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<PayrollRunDto> CalculateAsync(Guid id, CancellationToken ct)
    {
        var run = await runs.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Payroll run not found."); if (run.Status != PayrollRunStatus.Draft) throw new DomainException("Only draft payroll can be calculated.");
        var existing = await items.ListAsync(x => x.PayrollRunId == id, cancellationToken: ct); foreach (var old in existing) items.Remove(old);
        var workforce = await employees.ListAsync(x => x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod, cancellationToken: ct);
        foreach (var employee in workforce) { var gross = employee.BaseSalary; var item = new PayrollItem { TenantId = TenantId, PayrollRunId = run.Id, EmployeeId = employee.Id, BasicPay = employee.BaseSalary, GrossPay = gross, NetPay = gross }; await items.AddAsync(item, ct); run.GrossTotal += item.GrossPay; run.NetTotal += item.NetPay; }
        run.Status = PayrollRunStatus.Processing; await unitOfWork.SaveChangesAsync(ct); return Map(run);
    }
    public async Task<PayrollRunDto> ChangeStatusAsync(Guid id, PayrollRunStatus status, long version, CancellationToken ct) { var x = await runs.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Payroll run not found."); CheckVersion(x, version); var valid = (x.Status, status) is (PayrollRunStatus.Processing, PayrollRunStatus.Approved) or (PayrollRunStatus.Approved, PayrollRunStatus.Paid) or (_, PayrollRunStatus.Cancelled); if (!valid) throw new DomainException($"Payroll cannot move from {x.Status} to {status}."); x.Status = status; if (status == PayrollRunStatus.Paid) { var employeeIds = (await items.ListAsync(i => i.PayrollRunId == id, cancellationToken: ct)).Select(i => (Guid?)i.EmployeeId); await notifications.QueueForEmployeesAsync(employeeIds, "Salary paid", $"Payroll for {x.PeriodStart:dd MMM yyyy} to {x.PeriodEnd:dd MMM yyyy} has been marked paid.", "payroll", "/my-services", ct); } await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<PagedResult<PayrollRunDto>> SearchAsync(PagedRequest r, CancellationToken ct) { var total = await runs.CountAsync(cancellationToken: ct); var rows = await runs.ListAsync(orderBy: q => q.OrderByDescending(x => x.PeriodStart), skip: r.Skip, take: r.SafePageSize, cancellationToken: ct); return new(rows.Select(Map).ToArray(), r.SafePage, r.SafePageSize, total); }
    public async Task<IReadOnlyList<PayrollItemDto>> GetItemsAsync(Guid runId, CancellationToken ct) => (await items.ListAsync(x => x.PayrollRunId == runId, q => q.OrderBy(x => x.EmployeeId), cancellationToken: ct)).Select(x => new PayrollItemDto(x.Id, x.EmployeeId, x.BasicPay, x.Allowances, x.OvertimePay, x.Deductions, x.Taxes, x.GrossPay, x.NetPay)).ToArray();
    private static PayrollRunDto Map(PayrollRun x) => new(x.Id, x.Name, x.PeriodStart, x.PeriodEnd, x.PaymentDate, x.Status, x.Currency, x.GrossTotal, x.DeductionTotal, x.NetTotal, x.Version);
}

public sealed class RecruitmentService(IRepository<JobOpening> jobs, IRepository<Candidate> candidates, IRepository<JobApplication> applications, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), IRecruitmentService
{
    public async Task<JobDto> CreateJobAsync(CreateJobRequest r, CancellationToken ct) { if (await jobs.AnyAsync(x => x.Code == r.Code.ToUpper(), ct)) throw new DomainException("Job code already exists."); var x = new JobOpening { TenantId = TenantId, Title = r.Title.Trim(), Code = r.Code.Trim().ToUpperInvariant(), Description = r.Description, DepartmentId = r.DepartmentId, HiringManagerId = r.HiringManagerId, Openings = Math.Max(1, r.Openings), ClosesOn = r.ClosesOn, Status = JobStatus.Open }; await jobs.AddAsync(x, ct); await notifications.QueueForEmployeesAsync([x.HiringManagerId], "Hiring responsibility assigned", $"You are the hiring manager for {x.Title} ({x.Code}).", "recruitment", "/recruitment", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<CandidateDto> CreateCandidateAsync(CreateCandidateRequest r, CancellationToken ct) { var email = r.Email.Trim().ToLowerInvariant(); if (await candidates.AnyAsync(x => x.Email == email, ct)) throw new DomainException("Candidate email already exists."); var x = new Candidate { TenantId = TenantId, FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(), Email = email, Phone = r.Phone, ResumeStorageKey = r.ResumeStorageKey, Source = r.Source }; await candidates.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<CandidateDto>> ListCandidatesAsync(CancellationToken ct) => (await candidates.ListAsync(orderBy: q => q.OrderByDescending(x => x.CreatedAt), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<JobApplicationDto> ApplyAsync(ApplyCandidateRequest r, CancellationToken ct) { var job = await jobs.GetByIdAsync(r.JobOpeningId, ct) ?? throw new KeyNotFoundException("Job not found."); if (job.Status != JobStatus.Open) throw new DomainException("Job is not open."); var candidate = await candidates.GetByIdAsync(r.CandidateId, ct) ?? throw new KeyNotFoundException("Candidate not found."); if (await applications.AnyAsync(x => x.JobOpeningId == r.JobOpeningId && x.CandidateId == r.CandidateId, ct)) throw new DomainException("Candidate already applied to this job."); var x = new JobApplication { TenantId = TenantId, JobOpeningId = r.JobOpeningId, CandidateId = r.CandidateId, AppliedAt = DateTimeOffset.UtcNow }; await applications.AddAsync(x, ct); await notifications.QueueForEmployeesAsync([job.HiringManagerId], "New job application", $"{candidate.FirstName} {candidate.LastName} applied for {job.Title}.", "recruitment", "/recruitment", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<JobApplicationDto> MoveAsync(Guid id, MoveCandidateRequest r, CancellationToken ct) { var x = await applications.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Application not found."); x.Stage = r.Stage; x.Rating = r.Rating; x.Notes = r.Notes; await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<JobDto>> ListJobsAsync(JobStatus? status, CancellationToken ct) => (await jobs.ListAsync(x => !status.HasValue || x.Status == status, q => q.OrderByDescending(x => x.CreatedAt), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<IReadOnlyList<JobApplicationDto>> ListApplicationsAsync(Guid jobId, CancellationToken ct) => (await applications.ListAsync(x => x.JobOpeningId == jobId, q => q.OrderByDescending(x => x.AppliedAt), cancellationToken: ct)).Select(Map).ToArray();
    private static JobDto Map(JobOpening x) => new(x.Id, x.Title, x.Code, x.Openings, x.Status, x.ClosesOn); private static CandidateDto Map(Candidate x) => new(x.Id, x.FirstName, x.LastName, x.Email, x.Phone, x.Source); private static JobApplicationDto Map(JobApplication x) => new(x.Id, x.JobOpeningId, x.CandidateId, x.Stage, x.Rating, x.Notes, x.AppliedAt);
}

public sealed class PerformanceService(IRepository<PerformanceCycle> cycles, IRepository<PerformanceReview> reviews, IRepository<Employee> employees, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), IPerformanceService
{
    public async Task<Guid> CreateCycleAsync(CreatePerformanceCycleRequest r, CancellationToken ct) { if (r.EndsOn < r.StartsOn) throw new DomainException("Cycle dates are invalid."); var x = new PerformanceCycle { TenantId = TenantId, Name = r.Name.Trim(), StartsOn = r.StartsOn, EndsOn = r.EndsOn, IsActive = true }; await cycles.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return x.Id; }
    public async Task<PerformanceReviewDto> CreateReviewAsync(CreateReviewRequest r, CancellationToken ct) { var cycle = await cycles.GetByIdAsync(r.CycleId, ct) ?? throw new KeyNotFoundException("Cycle not found."); _ = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); _ = await employees.GetByIdAsync(r.ReviewerId, ct) ?? throw new KeyNotFoundException("Reviewer not found."); if (await reviews.AnyAsync(x => x.CycleId == r.CycleId && x.EmployeeId == r.EmployeeId && x.ReviewerId == r.ReviewerId, ct)) throw new DomainException("Review already exists."); var x = new PerformanceReview { TenantId = TenantId, CycleId = r.CycleId, EmployeeId = r.EmployeeId, ReviewerId = r.ReviewerId, GoalsJson = r.GoalsJson }; await reviews.AddAsync(x, ct); await notifications.QueueForEmployeesAsync([x.EmployeeId, x.ReviewerId], "Performance review assigned", $"A performance review was created for the {cycle.Name} cycle.", "performance", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<PerformanceReviewDto> UpdateReviewAsync(Guid id, UpdateReviewRequest r, CancellationToken ct) { var x = await reviews.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Review not found."); CheckVersion(x, r.Version); var previousStatus = x.Status; x.Status = r.Status; x.SelfRating = r.SelfRating; x.ManagerRating = r.ManagerRating; x.Feedback = r.Feedback; if (x.Status != previousStatus) await notifications.QueueForEmployeesAsync([x.EmployeeId, x.ReviewerId], "Performance review updated", $"The performance review moved from {previousStatus} to {x.Status}.", "performance", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<PerformanceReviewDto>> ListReviewsAsync(Guid? employeeId, Guid? cycleId, CancellationToken ct) => (await reviews.ListAsync(x => (!employeeId.HasValue || x.EmployeeId == employeeId) && (!cycleId.HasValue || x.CycleId == cycleId), q => q.OrderByDescending(x => x.CreatedAt), cancellationToken: ct)).Select(Map).ToArray();
    private static PerformanceReviewDto Map(PerformanceReview x) => new(x.Id, x.CycleId, x.EmployeeId, x.ReviewerId, x.Status, x.SelfRating, x.ManagerRating, x.GoalsJson, x.Feedback, x.Version);
}

public sealed class AssetService(IRepository<Asset> assets, IRepository<AssetAssignment> assignments, IRepository<Employee> employees, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), IAssetService
{
    public async Task<AssetDto> CreateAsync(CreateAssetRequest r, CancellationToken ct) { if (await assets.AnyAsync(x => x.AssetTag == r.AssetTag.ToUpper(), ct)) throw new DomainException("Asset tag already exists."); var x = new Asset { TenantId = TenantId, AssetTag = r.AssetTag.Trim().ToUpperInvariant(), Name = r.Name.Trim(), Category = r.Category.Trim(), SerialNumber = r.SerialNumber, PurchasedOn = r.PurchasedOn, PurchaseCost = r.PurchaseCost }; await assets.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<AssetDto> AssignAsync(Guid id, AssignAssetRequest r, CancellationToken ct) { var x = await assets.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Asset not found."); if (x.Status != AssetStatus.Available) throw new DomainException("Only available assets can be assigned."); _ = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); await assignments.AddAsync(new AssetAssignment { TenantId = TenantId, AssetId = id, EmployeeId = r.EmployeeId, AssignedAt = DateTimeOffset.UtcNow, Notes = r.Notes }, ct); x.Status = AssetStatus.Assigned; await notifications.QueueForEmployeesAsync([r.EmployeeId], "Asset assigned", $"{x.Name} ({x.AssetTag}) was assigned to you.", "asset", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<AssetDto> ReturnAsync(Guid id, CancellationToken ct) { var x = await assets.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Asset not found."); var assignment = await assignments.FirstOrDefaultAsync(a => a.AssetId == id && a.ReturnedAt == null, ct) ?? throw new DomainException("Asset has no active assignment."); assignment.ReturnedAt = DateTimeOffset.UtcNow; x.Status = AssetStatus.Available; await notifications.QueueForEmployeesAsync([assignment.EmployeeId], "Asset returned", $"{x.Name} ({x.AssetTag}) was marked as returned.", "asset", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<AssetDto>> ListAsync(AssetStatus? status, CancellationToken ct) => (await assets.ListAsync(x => !status.HasValue || x.Status == status, q => q.OrderBy(x => x.AssetTag), cancellationToken: ct)).Select(Map).ToArray();
    private static AssetDto Map(Asset x) => new(x.Id, x.AssetTag, x.Name, x.Category, x.SerialNumber, x.Status);
}

public sealed class ExpenseService(IRepository<ExpenseClaim> expenses, IRepository<Employee> employees, IRepository<StoredDocument> documents, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), IExpenseService
{
    public async Task<ExpenseDto> CreateAsync(CreateExpenseRequest r, CancellationToken ct) { _ = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); if (r.Amount <= 0) throw new DomainException("Expense amount must be positive."); var count = await expenses.CountAsync(cancellationToken: ct); var x = new ExpenseClaim { TenantId = TenantId, EmployeeId = r.EmployeeId, ClaimNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{count + 1:00000}", Category = r.Category, ExpenseDate = r.ExpenseDate, Amount = r.Amount, Currency = r.Currency.ToUpperInvariant(), Description = r.Description, ReceiptStorageKey = r.ReceiptStorageKey }; await expenses.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<ExpenseDto> SubmitAsync(Guid id, long version, CancellationToken ct) { var x = await expenses.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Expense not found."); CheckVersion(x, version); if (x.Status != ExpenseStatus.Draft) throw new DomainException("Only draft expenses can be submitted."); if (string.IsNullOrWhiteSpace(x.ReceiptStorageKey) && !await documents.AnyAsync(d => d.OwnerType == DocumentOwnerType.ExpenseClaim && d.OwnerId == x.Id && d.Category == "receipt", ct)) throw new DomainException("Attach at least one receipt or bill before submitting this expense claim."); x.Status = ExpenseStatus.Submitted; var employee = await employees.GetByIdAsync(x.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); await notifications.QueueForPermissionAsync(Permissions.ExpensesManage, "Expense submitted", $"{employee.FullName} submitted {x.ClaimNumber} for {x.Amount:0.00} {x.Currency}.", "expense", "/expenses", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<ExpenseDto> ReviewAsync(Guid id, ReviewExpenseRequest r, CancellationToken ct) { var x = await expenses.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Expense not found."); CheckVersion(x, r.Version); if (x.Status != ExpenseStatus.Submitted) throw new DomainException("Only submitted expenses can be reviewed."); x.Status = r.Approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected; x.ReviewedBy = user.UserId; await notifications.QueueForEmployeesAsync([x.EmployeeId], $"Expense {x.Status.ToString().ToLowerInvariant()}", $"Your expense claim {x.ClaimNumber} was {x.Status.ToString().ToLowerInvariant()}.", "expense", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<PagedResult<ExpenseDto>> SearchAsync(PagedRequest r, Guid? employeeId, ExpenseStatus? status, CancellationToken ct) { System.Linq.Expressions.Expression<Func<ExpenseClaim, bool>> p = x => (!employeeId.HasValue || x.EmployeeId == employeeId) && (!status.HasValue || x.Status == status); var total = await expenses.CountAsync(p, ct); var rows = await expenses.ListAsync(p, q => q.OrderByDescending(x => x.ExpenseDate), r.Skip, r.SafePageSize, ct); return new(rows.Select(Map).ToArray(), r.SafePage, r.SafePageSize, total); }
    private static ExpenseDto Map(ExpenseClaim x) => new(x.Id, x.EmployeeId, x.ClaimNumber, x.Category, x.ExpenseDate, x.Amount, x.Currency, x.Description, x.Status, x.Version);
}

public sealed class TrainingService(IRepository<TrainingCourse> courses, IRepository<TrainingEnrollment> enrollments, IRepository<Employee> employees, ICurrentTenant tenant, IUnitOfWork unitOfWork, INotificationService notifications) : ServiceBase(tenant), ITrainingService
{
    public async Task<TrainingCourseDto> CreateCourseAsync(CreateCourseRequest r, CancellationToken ct) { var x = new TrainingCourse { TenantId = TenantId, Title = r.Title.Trim(), Provider = r.Provider, Description = r.Description, DurationHours = r.DurationHours, IsMandatory = r.IsMandatory, ExpiresOn = r.ExpiresOn }; await courses.AddAsync(x, ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<TrainingEnrollmentDto> EnrollAsync(Guid courseId, EnrollEmployeeRequest r, CancellationToken ct) { var course = await courses.GetByIdAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found."); _ = await employees.GetByIdAsync(r.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found."); if (await enrollments.AnyAsync(x => x.CourseId == courseId && x.EmployeeId == r.EmployeeId && x.Status != EnrollmentStatus.Cancelled, ct)) throw new DomainException("Employee is already enrolled."); var x = new TrainingEnrollment { TenantId = TenantId, CourseId = courseId, EmployeeId = r.EmployeeId, EnrolledAt = DateTimeOffset.UtcNow }; await enrollments.AddAsync(x, ct); await notifications.QueueForEmployeesAsync([x.EmployeeId], "Training assigned", $"You were enrolled in {course.Title}.", "training", "/my-services", ct); await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<TrainingEnrollmentDto> CompleteAsync(Guid id, CompleteTrainingRequest r, CancellationToken ct) { var x = await enrollments.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Enrollment not found."); CheckVersion(x, r.Version); x.Status = EnrollmentStatus.Completed; x.CompletedAt = DateTimeOffset.UtcNow; x.Score = r.Score; await unitOfWork.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<TrainingCourseDto>> ListCoursesAsync(CancellationToken ct) => (await courses.ListAsync(orderBy: q => q.OrderBy(x => x.Title), cancellationToken: ct)).Select(Map).ToArray();
    public async Task<IReadOnlyList<TrainingEnrollmentDto>> ListEnrollmentsAsync(Guid? employeeId, CancellationToken ct) => (await enrollments.ListAsync(x => !employeeId.HasValue || x.EmployeeId == employeeId, q => q.OrderByDescending(x => x.EnrolledAt), cancellationToken: ct)).Select(Map).ToArray();
    private static TrainingCourseDto Map(TrainingCourse x) => new(x.Id, x.Title, x.Provider, x.DurationHours, x.IsMandatory, x.ExpiresOn); private static TrainingEnrollmentDto Map(TrainingEnrollment x) => new(x.Id, x.CourseId, x.EmployeeId, x.Status, x.EnrolledAt, x.CompletedAt, x.Score, x.Version);
}

public sealed class DashboardService(IRepository<Employee> employees, IRepository<LeaveRequest> leave, IRepository<JobOpening> jobs, IRepository<Asset> assets, IRepository<PayrollRun> payroll, ICurrentTenant tenant) : ServiceBase(tenant), IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct) { var active = await employees.CountAsync(x => x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod, ct); var pending = await leave.CountAsync(x => x.Status == LeaveRequestStatus.Pending, ct); var open = await jobs.CountAsync(x => x.Status == JobStatus.Open, ct); var available = await assets.CountAsync(x => x.Status == AssetStatus.Available, ct); var run = (await payroll.ListAsync(x => x.Status == PayrollRunStatus.Paid || x.Status == PayrollRunStatus.Approved, q => q.OrderByDescending(x => x.PeriodEnd), take: 1, cancellationToken: ct)).FirstOrDefault(); var grouped = (await employees.ListAsync(cancellationToken: ct)).GroupBy(x => x.Status.ToString()).ToDictionary(x => x.Key, x => x.Count()); return new(active, pending, open, available, run?.NetTotal ?? 0, grouped); }
}
