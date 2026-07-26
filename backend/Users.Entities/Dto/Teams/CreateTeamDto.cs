using System.ComponentModel.DataAnnotations;

namespace Users.Entities.Dto.Teams;

public sealed class CreateTeamDto
{
    [StringLength(64, MinimumLength = 1)]
    public required string Name { get; set; }
}
