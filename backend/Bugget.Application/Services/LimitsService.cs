using Bugget.Application.Errors;
using Bugget.Application.Ports;
using Bugget.Domain;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services;

public sealed class LimitsService(
    IAttachmentDbClient attachmentDbClient)
{
    private const int MaxAttachmentsCount = 10;

    public async Task<Error?> ValidateBugAttachmentLimitAsync(
        int reportId,
        int bugId,
        AttachType attachType)
    {
        if (attachType == AttachType.Expected || attachType == AttachType.Fact)
        {
            var bugAttachmentsCount = await attachmentDbClient.GetBugAttachmentsCountInternalAsync(
                reportId,
                bugId,
                (int)attachType);
            return bugAttachmentsCount < MaxAttachmentsCount
                ? null
                : BoErrors.AttachmentLimitExceeded;
        }
        else
        {
            return BoErrors.AttachmentTypeNotAllowed;
        }
    }

    public async Task<Error?> ValidateCommentAttachmentLimitAsync(
        string userId,
        int reportId,
        int bugId,
        int commentId)
    {
        var commentAttachmentsCount = await attachmentDbClient.GetCommentAttachmentsCountInternalAsync(
            userId,
            reportId,
            bugId,
            commentId);

        return commentAttachmentsCount < MaxAttachmentsCount
            ? null
            : BoErrors.AttachmentLimitExceeded;
    }

    public async Task<Error?> ValidateBugStepAttachmentLimitAsync(
        int reportId,
        int bugId,
        int stepId)
    {
        var stepAttachmentsCount = await attachmentDbClient.GetBugStepAttachmentsCountInternalAsync(
            reportId,
            bugId,
            stepId);

        return stepAttachmentsCount < MaxAttachmentsCount
            ? null
            : BoErrors.AttachmentLimitExceeded;
    }
}
