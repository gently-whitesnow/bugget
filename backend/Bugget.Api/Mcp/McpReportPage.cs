namespace Bugget.Api.Mcp;

/// <summary>
/// Проекции ответов read-инструментов. Имена полей и статусы совпадают с REST, состав — нет:
/// модель платит токенами за каждый вызов, поэтому список не тащит баги, а вложения отдают минимум.
/// Отброшено против REST: past_responsible_user_id и is_excluded_from_analytics (нужны аналитике),
/// report_id и bug_id у вложенных сущностей (родитель виден из дерева), updated_at у комментариев и шагов.
/// </summary>
internal sealed record McpReportPage(
    long Total,
    int Skip,
    int Take,
    McpReportListItem[] Reports);
