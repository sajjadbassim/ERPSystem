namespace ErpApi.Core.Interfaces;

public interface ICreationAuditable
{
    DateTime CreatedAt { get; set; }
    Guid CreatedByUserId { get; set; }
}
