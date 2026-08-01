namespace Bugget.Application.Users.Results.Users;

public sealed class AutocompleteUsersView
{
    public required IEnumerable<AutocompleteUserView> Users { get; init; }
    public required int Total { get; init; }
}
