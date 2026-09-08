using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record DocumentDto(Guid Id, DocumentOwnerType OwnerType, Guid OwnerId, string Category,
    string FileName, string Extension, string ContentType, long SizeBytes, DateTimeOffset CreatedAt, Guid? UploadedByUserId);
public sealed record DocumentContent(Stream Stream, string ContentType, string FileName);

public interface IDocumentStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentOwnerType ownerType, Guid ownerId, string? category, CancellationToken ct);
    Task<DocumentDto> UploadAsync(DocumentOwnerType ownerType, Guid ownerId, string category, string fileName,
        string contentType, long sizeBytes, Stream content, bool replace, CancellationToken ct);
    Task<DocumentContent> OpenAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class DocumentService(
    IRepository<StoredDocument> documents,
    IRepository<Employee> employees,
    IRepository<WorkItem> workItems,
    IRepository<WorkProjectMember> projectMembers,
    IRepository<LeaveRequest> leaveRequests,
    IRepository<ExpenseClaim> expenses,
    IRepository<Candidate> candidates,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IDocumentStorage storage,
    IUnitOfWork unitOfWork) : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    // Images
    ".jpg",
    ".jpeg",
    ".jpe",
    ".jpd",
    ".jfif",
    ".png",
    ".gif",
    ".webp",
    ".bmp",
    ".svg",
    ".avif",
    ".ico",
    ".heic",
    ".heif",
    ".tif",
    ".tiff",

    // PDF
    ".pdf",

    // Word / documents
    ".doc",
    ".docx",
    ".dotx",
    ".rtf",
    ".odt",

    // Excel / spreadsheets
    ".xls",
    ".xlsx",
    ".xlsm",
    ".xlsb",
    ".xltx",
    ".csv",
    ".ods",

    // Presentations
    ".ppt",
    ".pptx",
    ".odp",

    // Text / source / configuration
    ".txt",
    ".json",
    ".xml",
    ".log",
    ".md",
    ".yml",
    ".yaml",
    ".ini",
    ".sql",
    ".js",
    ".ts",
    ".css",
    ".html",
    ".htm",
    ".sh",
    ".conf",

    // Archives
    ".zip",
    ".7z",
    ".rar",

    // Audio
    ".mp3",
    ".wav",
    ".ogg",
    ".m4a",
    ".aac",

    // Video
    ".mp4",
    ".webm",
    ".mov",

    // Email
    ".eml",
    ".msg"
};

    private Guid TenantId => currentTenant.TenantId ?? throw new UnauthorizedAccessException("Tenant identity is missing.");

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentOwnerType ownerType, Guid ownerId, string? category, CancellationToken ct)
    {
        await EnsureAccessAsync(ownerType, ownerId, false, ct);
        var normalized = string.IsNullOrWhiteSpace(category) ? null : NormalizeCategory(category);
        var rows = await documents.ListAsync(x => x.OwnerType == ownerType && x.OwnerId == ownerId &&
            (normalized == null || x.Category == normalized), x => x.OrderByDescending(d => d.CreatedAt), cancellationToken: ct);
        return rows.Select(Map).ToArray();
    }

    public async Task<DocumentDto> UploadAsync(DocumentOwnerType ownerType, Guid ownerId, string category, string fileName,
        string contentType, long sizeBytes, Stream content, bool replace, CancellationToken ct)
    {
        await EnsureAccessAsync(ownerType, ownerId, true, ct);
        if (sizeBytes <= 0) throw new DomainException("The uploaded file cannot be empty.");
        var safeName = Path.GetFileName(fileName).Trim();
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeName) || !AllowedExtensions.Contains(extension))
            throw new DomainException("This file type is not supported.");

        var normalizedCategory = NormalizeCategory(category);
        if (ownerType is DocumentOwnerType.Employee or DocumentOwnerType.User && normalizedCategory == "profile")
        {
            var allowedProfileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp", ".avif"
            };
            if (!allowedProfileExtensions.Contains(extension))
                throw new DomainException("Profile photos must be JPG, PNG, WebP, or AVIF images.");
            replace = true;
        }
        var key = $"{TenantId:N}/{ownerType.ToString().ToLowerInvariant()}/{ownerId:N}/{Guid.NewGuid():N}{extension}";
        await storage.SaveAsync(key, content, ct);

        var row = new StoredDocument
        {
            TenantId = TenantId, OwnerType = ownerType, OwnerId = ownerId, Category = normalizedCategory,
            FileName = safeName, Extension = extension, ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream" : contentType, SizeBytes = sizeBytes, StorageKey = key,
            UploadedByUserId = currentUser.UserId
        };
        await documents.AddAsync(row, ct);
        var replaced = replace
            ? await documents.ListAsync(x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.Category == normalizedCategory, cancellationToken: ct)
            : [];
        foreach (var old in replaced.Where(x => x.Id != row.Id)) documents.Remove(old);
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var old in replaced.Where(x => x.Id != row.Id)) await storage.DeleteAsync(old.StorageKey, ct);
        return Map(row);
    }

    public async Task<DocumentContent> OpenAsync(Guid id, CancellationToken ct)
    {
        var row = await documents.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Document not found.");
        await EnsureAccessAsync(row.OwnerType, row.OwnerId, false, ct);
        return new(await storage.OpenReadAsync(row.StorageKey, ct), row.ContentType, row.FileName);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await documents.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Document not found.");
        await EnsureAccessAsync(row.OwnerType, row.OwnerId, true, ct);
        documents.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(row.StorageKey, ct);
    }

    private async Task EnsureAccessAsync(DocumentOwnerType ownerType, Guid ownerId, bool write, CancellationToken ct)
    {
        if (ownerId == Guid.Empty) throw new DomainException("A document owner is required.");
        switch (ownerType)
        {
            case DocumentOwnerType.Tenant:
                if (ownerId != TenantId || write && !currentUser.HasPermission(Permissions.IdentityManage)) Deny();
                break;
            case DocumentOwnerType.User:
                if (ownerId != currentUser.UserId && !currentUser.HasPermission(Permissions.IdentityManage)) Deny();
                break;
            case DocumentOwnerType.Employee:
                _ = await employees.GetByIdAsync(ownerId, ct) ?? throw new KeyNotFoundException("Employee not found.");
                if (ownerId != currentUser.EmployeeId && !currentUser.HasPermission(write ? Permissions.EmployeesManage : Permissions.EmployeesRead)) Deny();
                break;
            case DocumentOwnerType.WorkItem:
                if (!currentUser.HasPermission(Permissions.WorkRead)) Deny();
                var item = await workItems.GetByIdAsync(ownerId, ct) ?? throw new KeyNotFoundException("Work item not found.");
                if (!currentUser.HasPermission(Permissions.WorkManage))
                {
                    var employeeId = currentUser.EmployeeId ?? throw new UnauthorizedAccessException("Employee profile is required.");
                    if (!await projectMembers.AnyAsync(x => x.ProjectId == item.ProjectId && x.EmployeeId == employeeId, ct)) Deny();
                    if (write && !currentUser.HasPermission(Permissions.WorkComment)) Deny();
                }
                break;
            case DocumentOwnerType.LeaveRequest:
                var leave = await leaveRequests.GetByIdAsync(ownerId, ct) ?? throw new KeyNotFoundException("Leave request not found.");
                var leaveEmployee = await employees.GetByIdAsync(leave.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
                var ownsLeave = leave.EmployeeId == currentUser.EmployeeId;
                var managesLeave = currentUser.EmployeeId.HasValue && leaveEmployee.ManagerId == currentUser.EmployeeId && currentUser.HasPermission(Permissions.TeamRead);
                if (!ownsLeave && !currentUser.HasPermission(Permissions.LeaveManage) && !managesLeave) Deny();
                if (write && !ownsLeave && !currentUser.HasPermission(Permissions.LeaveManage)) Deny();
                if (write && leave.Status != LeaveRequestStatus.Pending) throw new DomainException("Documents cannot be changed after a leave request has been finalized.");
                break;
            case DocumentOwnerType.ExpenseClaim:
                var expense = await expenses.GetByIdAsync(ownerId, ct) ?? throw new KeyNotFoundException("Expense claim not found.");
                var expenseEmployee = await employees.GetByIdAsync(expense.EmployeeId, ct) ?? throw new KeyNotFoundException("Employee not found.");
                var ownsExpense = expense.EmployeeId == currentUser.EmployeeId;
                var managesExpense = currentUser.EmployeeId.HasValue && expenseEmployee.ManagerId == currentUser.EmployeeId && currentUser.HasPermission(Permissions.TeamRead);
                if (!ownsExpense && !currentUser.HasPermission(Permissions.ExpensesManage) && !managesExpense) Deny();
                if (write && !ownsExpense && !currentUser.HasPermission(Permissions.ExpensesManage)) Deny();
                if (write && expense.Status != ExpenseStatus.Draft) throw new DomainException("Receipts cannot be changed after an expense claim has been submitted.");
                break;
            case DocumentOwnerType.Candidate:
                _ = await candidates.GetByIdAsync(ownerId, ct) ?? throw new KeyNotFoundException("Candidate not found.");
                if (!currentUser.HasPermission(Permissions.RecruitmentManage)) Deny();
                break;
            default: Deny(); break;
        }
    }

    private static string NormalizeCategory(string category)
    {
        var value = new string(category.Trim().ToLowerInvariant().Where(x => char.IsLetterOrDigit(x) || x is '-' or '_').ToArray());
        if (value.Length is 0 or > 64) throw new DomainException("Document category is invalid.");
        return value;
    }

    private static void Deny() => throw new UnauthorizedAccessException("You do not have access to these documents.");
    private static DocumentDto Map(StoredDocument x) => new(x.Id, x.OwnerType, x.OwnerId, x.Category,
        x.FileName, x.Extension, x.ContentType, x.SizeBytes, x.CreatedAt, x.UploadedByUserId);
}
