namespace Bugget.Application.ExternalSearch.Models;

/// <summary>
/// Доска внешнего трекера в терминах приложения. Нейтральна к поставщику: Kaiten —
/// сегодняшняя реализация порта <c>IExternalBoardsClient</c>, а не форма этой модели.
/// </summary>
public sealed class ExternalBoard
{
    /// <summary>Идентификатор доски у поставщика.</summary>
    public required int Id { get; init; }

    /// <summary>Название доски.</summary>
    public required string Title { get; init; }

    /// <summary>Название пространства, которому доска принадлежит.</summary>
    public required string SpaceTitle { get; init; }

    /// <summary>Идентификатор пространства у поставщика.</summary>
    public required int SpaceId { get; init; }
}
