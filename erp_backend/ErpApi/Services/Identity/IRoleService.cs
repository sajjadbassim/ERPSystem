using ErpApi.Core.DTO.Roles;

namespace ErpApi.Services.Identity;

// خدمة رابعة خارج الثلاث المسمّاة في الخطوة السابعة، أوجبها الحارسان H01 و H02:
// حذف الدور النظامي وإعادة تسميته يجب أن يُرفضا بـ 409
public interface IRoleService
{
    Task DeleteAsync(Guid roleId, CancellationToken ct = default);

    Task<RoleResponseDto> RenameAsync(Guid roleId, RenameRoleRequestDto request, CancellationToken ct = default);
}
