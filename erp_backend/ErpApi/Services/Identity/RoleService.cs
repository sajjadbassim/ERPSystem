using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Roles;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Repositories;

namespace ErpApi.Services.Identity;

public class RoleService : IRoleService
{
    private const string SystemRoleImmutable = "الدور النظامي لا يُحذف ولا يُعاد تسميته.";
    private const string RoleNotFound = "الدور غير موجود.";

    private readonly IRoleRepository _roleRepository;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(IRoleRepository roleRepository, IUserService userService, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await LoadManageableAsync(roleId, ct);

        // حذف منطقي: الدور لا يشير إليه سطر مالي مرحَّل فيبقى على حكم R-LIFE-01.
        // أما صفوف UserRoles فتُحذف فعلياً عند سحب الدور من مستخدم (قرار §4.1)
        role.IsDeleted = true;
        _roleRepository.Update(role);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<RoleResponseDto> RenameAsync(
        Guid roleId, RenameRoleRequestDto request, CancellationToken ct = default)
    {
        var role = await LoadManageableAsync(roleId, ct);

        role.Name = request.Name;
        _roleRepository.Update(role);

        await _unitOfWork.SaveChangesAsync(ct);

        return new RoleResponseDto
        {
            Id = role.Id,
            Code = role.Code,
            Name = role.Name,
            IsSystemRole = role.IsSystemRole,
            IsActive = role.IsActive
        };
    }

    private async Task<Core.Models.Role> LoadManageableAsync(Guid roleId, CancellationToken ct)
    {
        // الصلاحية أولاً ثم الوجود: العكس يكشف أي المعرّفات حقيقي لمن لا يملك الصلاحية
        await _userService.EnsurePermissionAsync(Permissions.RoleManage, ct);

        var role = await _roleRepository.GetByIdAsync(roleId, ct)
            ?? throw new NotFoundException(RoleNotFound);

        // تعارض حالة لا خطأ مدخلات، فهو 409 لا 400 (بند 8.1)
        if (role.IsSystemRole)
        {
            throw new ConflictException(SystemRoleImmutable);
        }

        return role;
    }
}
