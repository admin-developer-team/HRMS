using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Tests;

public sealed class DocumentUploadTests
{
    [Fact]
    public async Task Upload_accepts_files_above_the_previous_application_limits()
    {
        var tenant = new CurrentTenant();
        var tenantId = Guid.NewGuid();
        tenant.Set(tenantId);
        var user = new TestCurrentUser { IsPlatformAdmin = true };
        await using var db = new HrmsDbContext(
            new DbContextOptionsBuilder<HrmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenant, user, new TestNotificationPublisher());
        var storage = new RecordingStorage();
        var service = new DocumentService(
            new Repository<StoredDocument>(db), new Repository<Employee>(db), new Repository<WorkItem>(db),
            new Repository<WorkProjectMember>(db), new Repository<LeaveRequest>(db), new Repository<ExpenseClaim>(db),
            new Repository<Candidate>(db), tenant, user, storage, db);

        var uploaded = await service.UploadAsync(DocumentOwnerType.Tenant, tenantId, "logo", "large-logo.png",
            "image/png", 25L * 1024 * 1024, Stream.Null, true, CancellationToken.None);

        Assert.Equal(25L * 1024 * 1024, uploaded.SizeBytes);
        Assert.NotNull(storage.SavedKey);
    }

    private sealed class RecordingStorage : IDocumentStorage
    {
        public string? SavedKey { get; private set; }
        public Task SaveAsync(string key, Stream content, CancellationToken ct)
        {
            SavedKey = key;
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(Stream.Null);
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
    }
}
