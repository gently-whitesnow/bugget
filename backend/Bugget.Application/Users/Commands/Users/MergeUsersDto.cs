using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Users.Commands.Users;

public sealed class MergeUsersDto
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public required string SourceUserId { get; set; }
}
