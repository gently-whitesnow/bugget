using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Users.Dto.Users;

public sealed class LinkMattermostDto
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string MattermostUserId { get; set; }
}
