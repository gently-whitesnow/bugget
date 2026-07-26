using System.ComponentModel.DataAnnotations;

namespace Users.Entities.Dto.Users;

public sealed class PutUserDto
{
    [StringLength(256, MinimumLength = 1)]
    public required string Name { get; set; }
}
