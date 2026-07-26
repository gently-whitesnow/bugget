namespace Bugget.Entities.DbModels.Settings;

public sealed class TeamSettingDbModel
{
    public required int Id { get; init; }
    public required string TeamId { get; init; }
    public required string FeatureKey { get; init; }
    public required string FieldKey { get; init; }
    public required string FieldValue { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

