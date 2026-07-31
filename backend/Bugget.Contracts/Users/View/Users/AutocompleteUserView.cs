namespace Bugget.Contracts.Users.View.Users;

public sealed class AutocompleteUserView
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? ImageUrl { get; set; }
}
