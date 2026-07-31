using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Users.Dto.Users;

public sealed class PutUserDto
{
    [StringLength(256, MinimumLength = 1)]
    public required string Name { get; set; }
}
