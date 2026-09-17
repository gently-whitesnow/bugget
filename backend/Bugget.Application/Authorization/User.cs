using System;

namespace Bugget.Application.Authorization;

public sealed class User
{
    public long Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTimeOffset RegistrationDate { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
