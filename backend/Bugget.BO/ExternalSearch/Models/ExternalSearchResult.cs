using Bugget.BO.ExternalSearch.Models;

public sealed class ExternalSearchResult
{
    public required long Total { get; init; }
    public required ICollection<IExternalSearchItem> Items { get; init; }
}
