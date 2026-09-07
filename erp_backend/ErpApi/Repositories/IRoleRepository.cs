using ErpApi.Core.Models;

namespace ErpApi.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);

    void Update(Role role);
}
