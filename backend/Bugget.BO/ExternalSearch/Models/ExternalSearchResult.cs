namespace Bugget.BO.ExternalSearch.Models;

// Namespace объявлен явно: без него тип лежал в глобальном пространстве имён и
// перекрывал одноимённую контрактную схему в сгенерированном коде.
public sealed class ExternalSearchResult
{
    public required long Total { get; init; }
    public required ICollection<IExternalSearchItem> Items { get; init; }
}
