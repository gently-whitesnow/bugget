using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Users.Dto.Teams;

public sealed class CreateTeamDto
{
    [StringLength(64, MinimumLength = 1)]
    public required string Name { get; set; }
}
