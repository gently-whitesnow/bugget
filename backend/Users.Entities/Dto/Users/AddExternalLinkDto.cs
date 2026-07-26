using System.ComponentModel.DataAnnotations;

namespace Users.Entities.Dto.Users;

public sealed class AddExternalLinkDto
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string Provider { get; set; }

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public required string ExternalId { get; set; }

    [StringLength(256)]
    public string? Email { get; set; }
}
