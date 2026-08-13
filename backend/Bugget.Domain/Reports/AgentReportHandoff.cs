namespace Bugget.Domain.Reports;

/// <summary>
/// Кому переходит ответственность за репорт, когда статус меняет агент.
/// Агент не пользователь и ответственным быть не может: пока идут правки (Fix)
/// репорт держит владелец PAT, а после пуша (Test) репорт возвращается
/// тестировщику — прежнему ответственному, или автору репорта, если
/// ответственного не было.
/// </summary>
public static class AgentReportHandoff
{
    /// <summary>
    /// Новый <c>responsible_user_id</c> для перехода в <paramref name="targetStatus"/>,
    /// или <c>null</c>, когда ответственного менять не нужно: целевой статус не
    /// Fix/Test, репорт уже у нужного человека, либо в Test репорт держит другой
    /// человек — у живого ответственного репорт не отбирается.
    /// </summary>
    public static string? ResolveResponsible(
        ReportStatus targetStatus,
        string tokenOwnerUserId,
        string? responsibleUserId,
        string? pastResponsibleUserId,
        string creatorUserId)
    {
        switch (targetStatus)
        {
            case ReportStatus.Fix:
                return string.Equals(responsibleUserId, tokenOwnerUserId, StringComparison.Ordinal)
                    ? null
                    : tokenOwnerUserId;

            case ReportStatus.Test:
                if (responsibleUserId is not null
                    && !string.Equals(responsibleUserId, tokenOwnerUserId, StringComparison.Ordinal))
                {
                    return null;
                }

                var tester = pastResponsibleUserId ?? creatorUserId;
                return string.Equals(tester, responsibleUserId, StringComparison.Ordinal)
                    ? null
                    : tester;

            default:
                return null;
        }
    }
}
