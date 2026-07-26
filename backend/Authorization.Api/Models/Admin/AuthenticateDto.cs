using System.ComponentModel.DataAnnotations;
using Authorization.Abstractions;

namespace Authorization.Api.Models.Admin;

public sealed class AuthenticateDto : IExternalUser
{
    [StringLength(255, MinimumLength = 1)]
    public required string Name { get; set; }
    public required string ExternalId { get; set; }
    public string? ImageUrl { get; set; }
}
