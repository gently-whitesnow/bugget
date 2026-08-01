using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Users.Commands.Workspaces;

public sealed class CreateWorkspaceDto
{
    [StringLength(32, MinimumLength = 1)]
    public required string Name { get; set; }
}
