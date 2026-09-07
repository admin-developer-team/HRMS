using Hrms.Application;
using Hrms.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/payroll"), Authorize(Policy = Permissions.PayrollManage)]
public sealed class PayrollController(IPayrollService service) : ControllerBase
{
    [HttpPost("runs")] public Task<PayrollRunDto> Create(CreatePayrollRunRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPost("runs/{id:guid}/calculate")] public Task<PayrollRunDto> Calculate(Guid id, CancellationToken ct) => service.CalculateAsync(id, ct);
    [HttpPut("runs/{id:guid}/status")] public Task<PayrollRunDto> Status(Guid id, [FromQuery] PayrollRunStatus status, [FromQuery] long version, CancellationToken ct) => service.ChangeStatusAsync(id, status, version, ct);
    [HttpGet("runs")] public Task<PagedResult<PayrollRunDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize), ct);
    [HttpGet("runs/{id:guid}/items")] public Task<IReadOnlyList<PayrollItemDto>> Items(Guid id, CancellationToken ct) => service.GetItemsAsync(id, ct);
}

[ApiController, Route("api/v1/recruitment"), Authorize(Policy = Permissions.RecruitmentManage)]
public sealed class RecruitmentController(IRecruitmentService service) : ControllerBase
{
    [HttpPost("jobs")] public Task<JobDto> CreateJob(CreateJobRequest request, CancellationToken ct) => service.CreateJobAsync(request, ct);
    [HttpGet("jobs")] public Task<IReadOnlyList<JobDto>> Jobs([FromQuery] JobStatus? status = null, CancellationToken ct = default) => service.ListJobsAsync(status, ct);
    [HttpPost("candidates")] public Task<CandidateDto> CreateCandidate(CreateCandidateRequest request, CancellationToken ct) => service.CreateCandidateAsync(request, ct);
    [HttpGet("candidates")] public Task<IReadOnlyList<CandidateDto>> Candidates(CancellationToken ct) => service.ListCandidatesAsync(ct);
    [HttpPost("applications")] public Task<JobApplicationDto> Apply(ApplyCandidateRequest request, CancellationToken ct) => service.ApplyAsync(request, ct);
    [HttpPut("applications/{id:guid}/stage")] public Task<JobApplicationDto> Move(Guid id, MoveCandidateRequest request, CancellationToken ct) => service.MoveAsync(id, request, ct);
    [HttpGet("jobs/{jobId:guid}/applications")] public Task<IReadOnlyList<JobApplicationDto>> Applications(Guid jobId, CancellationToken ct) => service.ListApplicationsAsync(jobId, ct);
}

[ApiController, Route("api/v1/performance"), Authorize(Policy = Permissions.PerformanceManage)]
public sealed class PerformanceController(IPerformanceService service) : ControllerBase
{
    [HttpPost("cycles")] public Task<Guid> CreateCycle(CreatePerformanceCycleRequest request, CancellationToken ct) => service.CreateCycleAsync(request, ct);
    [HttpPost("reviews")] public Task<PerformanceReviewDto> CreateReview(CreateReviewRequest request, CancellationToken ct) => service.CreateReviewAsync(request, ct);
    [HttpPut("reviews/{id:guid}")] public Task<PerformanceReviewDto> UpdateReview(Guid id, UpdateReviewRequest request, CancellationToken ct) => service.UpdateReviewAsync(id, request, ct);
    [HttpGet("reviews")] public Task<IReadOnlyList<PerformanceReviewDto>> Reviews([FromQuery] Guid? employeeId = null, [FromQuery] Guid? cycleId = null, CancellationToken ct = default) => service.ListReviewsAsync(employeeId, cycleId, ct);
}

[ApiController, Route("api/v1/assets"), Authorize(Policy = Permissions.AssetsManage)]
public sealed class AssetsController(IAssetService service) : ControllerBase
{
    [HttpPost] public Task<AssetDto> Create(CreateAssetRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPost("{id:guid}/assign")] public Task<AssetDto> Assign(Guid id, AssignAssetRequest request, CancellationToken ct) => service.AssignAsync(id, request, ct);
    [HttpPost("{id:guid}/return")] public Task<AssetDto> Return(Guid id, CancellationToken ct) => service.ReturnAsync(id, ct);
    [HttpGet] public Task<IReadOnlyList<AssetDto>> List([FromQuery] AssetStatus? status = null, CancellationToken ct = default) => service.ListAsync(status, ct);
}

[ApiController, Route("api/v1/expenses"), Authorize(Policy = Permissions.ExpensesManage)]
public sealed class ExpensesController(IExpenseService service) : ControllerBase
{
    [HttpPost] public Task<ExpenseDto> Create(CreateExpenseRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPost("{id:guid}/submit")] public Task<ExpenseDto> Submit(Guid id, [FromQuery] long version, CancellationToken ct) => service.SubmitAsync(id, version, ct);
    [HttpPut("{id:guid}/review")] public Task<ExpenseDto> Review(Guid id, ReviewExpenseRequest request, CancellationToken ct) => service.ReviewAsync(id, request, ct);
    [HttpGet] public Task<PagedResult<ExpenseDto>> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? employeeId = null, [FromQuery] ExpenseStatus? status = null, CancellationToken ct = default) => service.SearchAsync(new(page, pageSize), employeeId, status, ct);
}

[ApiController, Route("api/v1/training"), Authorize(Policy = Permissions.TrainingManage)]
public sealed class TrainingController(ITrainingService service) : ControllerBase
{
    [HttpPost("courses")] public Task<TrainingCourseDto> CreateCourse(CreateCourseRequest request, CancellationToken ct) => service.CreateCourseAsync(request, ct);
    [HttpGet("courses")] public Task<IReadOnlyList<TrainingCourseDto>> Courses(CancellationToken ct) => service.ListCoursesAsync(ct);
    [HttpPost("courses/{courseId:guid}/enroll")] public Task<TrainingEnrollmentDto> Enroll(Guid courseId, EnrollEmployeeRequest request, CancellationToken ct) => service.EnrollAsync(courseId, request, ct);
    [HttpPut("enrollments/{id:guid}/complete")] public Task<TrainingEnrollmentDto> Complete(Guid id, CompleteTrainingRequest request, CancellationToken ct) => service.CompleteAsync(id, request, ct);
    [HttpGet("enrollments")] public Task<IReadOnlyList<TrainingEnrollmentDto>> Enrollments([FromQuery] Guid? employeeId = null, CancellationToken ct = default) => service.ListEnrollmentsAsync(employeeId, ct);
}
