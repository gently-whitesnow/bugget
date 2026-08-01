namespace Bugget.Infrastructure.ExternalClients.Notifications;

public static class ReportMessageBuilder
{
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
