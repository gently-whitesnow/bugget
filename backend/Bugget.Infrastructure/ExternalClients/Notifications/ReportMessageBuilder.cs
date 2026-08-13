namespace Bugget.Infrastructure.ExternalClients.Notifications;

public static class ReportMessageBuilder
{
    /// <summary>
    /// Подпись инициатора-агента. Имени у агента нет: токен принадлежит человеку,
    /// но действие — не его, поэтому в уведомлении стоит то же «Агент», что и в
    /// подписи на странице репорта (kaiten 237718).
    /// </summary>
    public const string AgentInitiatorName = "Агент";

    private static readonly string? BuggetBaseUrl = Environment.GetEnvironmentVariable(NotificationsConstants.BuggetBaseUrlKey);

    public static string GetYourResponsibleAfterPatchReportMessage(string aliasId, string? teamId, string reportTitle, string creatorUserFullName)
    {
        var reportPath = string.IsNullOrEmpty(teamId)
            ? $"/reports/{aliasId}"
            : $"/teams/{teamId}/reports/{aliasId}";

        return $":arrow_forward: Вы назначены ответственным **[в баг-репорте]({BuggetBaseUrl}{reportPath})**\n" +
               $"Название: **{reportTitle}**\n" +
               $"Инициатор: {creatorUserFullName}";
    }
}
