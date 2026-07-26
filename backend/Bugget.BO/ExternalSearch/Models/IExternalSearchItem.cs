namespace Bugget.BO.ExternalSearch.Models;

public interface IExternalSearchItem
{
    /// <summary>
    /// Идентификатор элемента
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Текст элемента по которому идет поиск (Задача такая-то)
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Источник элемента (Kaiten, Jira, GitLab, etc.)
    string Source { get; }
}
