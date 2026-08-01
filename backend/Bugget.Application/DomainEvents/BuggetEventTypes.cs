namespace Bugget.Application.DomainEvents;

public static class BuggetEventTypes
{
    public const string BugCreated = "bugget.bug.created";
    public const string BugStatusChanged = "bugget.bug.status_changed";
    public const string CommentCreated = "bugget.comment.created";
    public const string AttachmentCreated = "bugget.attachment.created";

    public const string ReportStatusChanged = "bugget.report.status_changed";

    public const string ReportExcludedFromAnalyticsToggled = "bugget.report.excluded_from_analytics_toggled";
}
