using System.ComponentModel.DataAnnotations;

namespace Users.Entities.Dto.Workspaces;

public sealed class CreateWorkspaceDto
{
    [StringLength(32, MinimumLength = 1)]
    public required string Name { get; set; }
}
