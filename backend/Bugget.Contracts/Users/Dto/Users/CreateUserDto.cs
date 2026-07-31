using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Users.Dto.Users;

public sealed class CreateUserDto
{
    [StringLength(256, MinimumLength = 1)]
    public required string ExternalId { get; set; }
    [StringLength(256)]
    public string? Name { get; set; }
    [StringLength(512)]
    public string? ImageUrl { get; set; }
}
