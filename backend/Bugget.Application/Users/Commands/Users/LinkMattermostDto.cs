using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Users.Commands.Users;

public sealed class LinkMattermostDto
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string MattermostUserId { get; set; }
}
