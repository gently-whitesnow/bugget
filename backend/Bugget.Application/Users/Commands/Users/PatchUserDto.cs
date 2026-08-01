using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Users.Commands.Users;

public sealed class PutUserDto
{
    [StringLength(256, MinimumLength = 1)]
    public required string Name { get; set; }
}
