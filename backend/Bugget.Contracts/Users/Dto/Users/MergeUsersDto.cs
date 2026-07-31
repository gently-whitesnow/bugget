using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Users.Dto.Users;

public sealed class MergeUsersDto
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public required string SourceUserId { get; set; }
}
