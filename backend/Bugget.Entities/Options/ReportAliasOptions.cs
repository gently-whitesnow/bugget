namespace Bugget.Entities.Options;

public sealed class ReportAliasOptions
{
    public required string AliasMode { get; set; }
}
public static class ReportAliasMode
{
    public const string Default = "default";
    public const string Guid = "guid";
    public const string Team = "team";
}
