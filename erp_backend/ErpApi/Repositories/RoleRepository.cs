using ErpApi.Core.Models;
using ErpApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context) => _context = context;

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Update(Role role) => _context.Roles.Update(role);
}
