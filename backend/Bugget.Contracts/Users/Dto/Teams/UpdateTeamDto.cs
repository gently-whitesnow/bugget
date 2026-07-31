using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Users.Dto.Teams;

public sealed class UpdateTeamDto
{
    [StringLength(64, MinimumLength = 1)]
    public required string Name { get; set; }
}
