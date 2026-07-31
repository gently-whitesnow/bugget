namespace Bugget.Contracts.Users.View.Users;

public sealed class AutocompleteUsersView
{
    public required IEnumerable<AutocompleteUserView> Users { get; init; }
    public required int Total { get; init; }
}
