namespace Users.Entities.Options;

public sealed class TeamsOptions
{
    public required TimeSpan ExpiresIn { get; set; }

    public required int DefaultSizeLimit { get; set; }
    public required int DefaultTeamsCountLimit { get; set; }
    public required string Pepper { get; set; }

}
