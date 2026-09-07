using System.ComponentModel.DataAnnotations;

namespace ErpApi.Core.DTO.Roles;

public class RenameRoleRequestDto
{
    [Required(ErrorMessage = "اسم الدور مطلوب.")]
    [MaxLength(200, ErrorMessage = "اسم الدور أطول من الحد المسموح.")]
    public string Name { get; set; } = string.Empty;
}

public class RoleResponseDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsSystemRole { get; init; }
    public required bool IsActive { get; init; }
}
