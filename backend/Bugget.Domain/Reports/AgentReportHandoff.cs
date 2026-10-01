namespace Bugget.Domain.Reports;

/// <summary>
/// Кому переходит репорт, когда статус меняет агент (агент ответственным быть не может): в Fix его держит
/// владелец PAT, в Test он возвращается прежнему ответственному или автору, если ответственного не было.
/// </summary>
public static class AgentReportHandoff
{
    /// <summary>Новый <c>responsible_user_id</c> для <paramref name="targetStatus"/> или <c>null</c>: статус не Fix/Test,
    /// репорт уже у нужного человека, либо в Test его держит другой — у живого ответственного не отбираем.</summary>
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
