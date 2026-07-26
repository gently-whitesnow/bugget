using System.ComponentModel.DataAnnotations;

namespace Users.Entities.Dto.Users;

public sealed class MergeUsersDto
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public required string SourceUserId { get; set; }
}
