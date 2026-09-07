namespace Hrms.Domain.Common;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long Version { get; set; }
}

public abstract class TenantEntity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}

public sealed class DomainException(string message) : Exception(message);

