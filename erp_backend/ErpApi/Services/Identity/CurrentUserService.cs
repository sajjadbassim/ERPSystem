using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;

namespace ErpApi.Services.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => ReadGuid(AppClaimTypes.UserId);

    public Guid? CompanyId => ReadGuid(AppClaimTypes.CompanyId);

    public Guid? SecurityStamp => ReadGuid(AppClaimTypes.SecurityStamp);

    private Guid? ReadGuid(string claimType)
    {
        var raw = _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(raw, out var value) ? value : null;
    }
}
