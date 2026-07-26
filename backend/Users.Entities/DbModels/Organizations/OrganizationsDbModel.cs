using System;

namespace Users.Entities.DbModels.Organizations;

public sealed class OrganizationsDbModel
{
    public int Id { get; set; }
    public long OwnerUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
