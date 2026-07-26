using System.ComponentModel.DataAnnotations;

namespace Users.Entities.Dto.Teams;

public sealed class UpdateTeamDto
{
    [StringLength(64, MinimumLength = 1)]
    public required string Name { get; set; }
}
