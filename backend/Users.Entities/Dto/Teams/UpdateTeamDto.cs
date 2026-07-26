namespace Users.Entities.Dto.Teams;

using System.ComponentModel.DataAnnotations;

public sealed class UpdateTeamDto
{
    [StringLength(64, MinimumLength = 1)]
    public required string Name { get; set; }
}
