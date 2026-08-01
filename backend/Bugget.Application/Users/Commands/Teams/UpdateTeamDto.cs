using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Users.Commands.Teams;

public sealed class UpdateTeamDto
{
    [StringLength(64, MinimumLength = 1)]
    public required string Name { get; set; }
}
