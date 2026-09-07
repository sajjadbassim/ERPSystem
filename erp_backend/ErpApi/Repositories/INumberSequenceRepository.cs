using ErpApi.Core.Constants;
using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface INumberSequenceRepository
{
    Task<bool> ExistsForBranchAsync(
        Guid branchId, SequenceDocumentType documentType, CancellationToken ct = default);

    Task<List<NumberSequence>> GetByBranchAsync(Guid branchId, CancellationToken ct = default);

    Task AddAsync(NumberSequence entity, CancellationToken ct = default);
}
