namespace Bugget.Api.ExternalSearch;

public sealed class ExternalSearchItemView
{
    /// <summary>
    /// Идентификатор элемента
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Текст элемента по которому идет поиск (Задача такая-то)
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Источник элемента (Kaiten, Jira, GitLab, etc.)
    /// </summary>
    public required string Source { get; init; }
}
