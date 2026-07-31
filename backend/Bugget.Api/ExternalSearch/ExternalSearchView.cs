namespace Bugget.Api.ExternalSearch;

public sealed class ExternalSearchView
{
    public required long Total { get; init; }
    public required ExternalSearchItemView[] Items { get; init; }
}
