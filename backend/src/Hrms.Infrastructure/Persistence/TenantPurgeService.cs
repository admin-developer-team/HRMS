using System.Data;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hrms.Infrastructure.Persistence;

/// <summary>Irreversible platform-only tenant removal; bypasses the normal soft-delete interceptor.</summary>
public sealed class TenantPurgeService(HrmsDbContext db, LocalDocumentStorage storage, ILogger<TenantPurgeService> logger)
{
    public async Task DeleteAsync(Guid id, string confirmationSlug, CancellationToken ct)
    {
        if (id == DatabaseInitializer.PlatformTenantId)
            throw new DomainException("The platform workspace cannot be deleted.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == id)
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Company not found.");
        if (tenant.IsDeleted || !string.Equals(tenant.Slug, confirmationSlug?.Trim(), StringComparison.Ordinal))
            throw new DomainException("Enter the exact workspace URL prefix to confirm permanent deletion.");

        // Do not erase the only local reference to a recurring mandate while the provider can still charge it.
        var checkouts = await db.BillingCheckouts.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == id).Select(x => new { x.ProviderSubscriptionId, x.Status }).ToListAsync(ct);
        var mandateIds = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == id && x.ProviderSubscriptionId != null)
            .Select(x => x.ProviderSubscriptionId!).ToListAsync(ct);
        if (checkouts.Any(x => !string.IsNullOrWhiteSpace(x.ProviderSubscriptionId)
                && x.Status.ToLower() is not ("cancelled" or "canceled" or "completed" or "expired"))
            || mandateIds.Any(mandate => !checkouts.Any(x => x.ProviderSubscriptionId == mandate
                && x.Status.ToLower() is ("cancelled" or "canceled" or "completed" or "expired"))))
            throw new DomainException("Cancel this company's recurring payment mandate at the payment provider before deleting its records.");

        var prefix = $"{id:N}/";
        var keys = new List<string>();
        keys.AddRange(await db.StoredDocuments.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == id).Select(x => x.StorageKey).ToListAsync(ct));
        keys.AddRange(await db.EmployeeDocuments.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == id).Select(x => x.StorageKey).ToListAsync(ct));
        keys.AddRange((await db.Candidates.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == id).Select(x => x.ResumeStorageKey).ToListAsync(ct))!);
        keys.AddRange((await db.ExpenseClaims.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == id).Select(x => x.ReceiptStorageKey).ToListAsync(ct))!);

        // Support requests are held under the platform tenant, but contain this company's contact data.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SupportTickets\" WHERE \"SourceTenantId\" = @tenantId",
            [new NpgsqlParameter("tenantId", id)], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditLogs\" WHERE \"TenantId\" = @platformId AND \"EntityType\" = 'Tenant' AND \"EntityId\" = @entityId",
            [new NpgsqlParameter("platformId", DatabaseInitializer.PlatformTenantId), new NpgsqlParameter("entityId", id.ToString())], ct);

        var entityTypes = db.Model.GetEntityTypes().Where(x => typeof(ITenantEntity).IsAssignableFrom(x.ClrType)).ToArray();
        var ordered = new List<IEntityType>();
        var visited = new HashSet<IEntityType>();
        void Visit(IEntityType entity)
        {
            if (!visited.Add(entity)) return;
            foreach (var dependent in entityTypes.Where(x => x.GetForeignKeys().Any(f => f.PrincipalEntityType == entity)))
                Visit(dependent);
            ordered.Add(entity);
        }
        foreach (var entity in entityTypes) Visit(entity);
        foreach (var entity in ordered)
        {
            var table = entity.GetTableName();
            if (table is null) continue;
            var schema = entity.GetSchema();
            var tableId = StoreObjectIdentifier.Table(table, schema);
            var column = entity.FindProperty(nameof(ITenantEntity.TenantId))?.GetColumnName(tableId);
            if (column is null) throw new InvalidOperationException($"TenantId mapping missing for {entity.Name}.");
            static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
            var qualifiedTable = schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
            var statement = string.Concat("DELETE FROM ", qualifiedTable, " WHERE ", Quote(column), " = @tenantId");
            await db.Database.ExecuteSqlRawAsync(statement,
                [new NpgsqlParameter("tenantId", id)], ct);
        }
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Tenants\" WHERE \"Id\" = @tenantId",
            [new NpgsqlParameter("tenantId", id)], ct);
        await transaction.CommitAsync(ct);

        foreach (var key in keys.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            // Imported legacy keys may be shared or point outside this tenant's directory.
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            try { await storage.DeleteAsync(key, ct); }
            catch (Exception error) { logger.LogError(error, "Could not remove document {Key} after deleting tenant {TenantId}", key, id); }
        }
        try { storage.DeleteTenantDirectory(id); }
        catch (Exception error) { logger.LogError(error, "Could not remove document directory after deleting tenant {TenantId}", id); }
    }
}
