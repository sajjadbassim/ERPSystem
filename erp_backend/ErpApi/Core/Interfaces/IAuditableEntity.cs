namespace ErpApi.Core.Interfaces;

public interface IAuditableEntity : ICreationAuditable
{
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedByUserId { get; set; }
}
