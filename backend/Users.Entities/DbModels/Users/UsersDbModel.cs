namespace Users.Entities.DbModels.Users;

public sealed class UsersDbModel
{
    public required UserDbModel[] Users { get; init; }
    public required int Total { get; init; }
}
