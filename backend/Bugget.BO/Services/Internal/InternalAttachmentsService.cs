using Bugget.BO.Errors;
using Bugget.BO.Services.Attachments;
using Bugget.DA.Interfaces;
using Bugget.Entities.BO;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.DbModels.Attachment;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// POST /v2/_internal/attachments?bugId=|commentId=: multipart-аплоад attachment'ов от
/// beta-bot'а к Bug'у (создание репорта) или Comment'у (диалог тестера). Контракт —
/// TECHSPEC §4.3.2/§4.3.3. Caller передаёт ровно один из {bugId, commentId};
/// workspace+report+alias резолвятся по нему. Дальше — общий `AttachmentService`
/// pipeline (валидация/лимиты/storage/events).
/// </summary>
public sealed class InternalAttachmentsService(
    IBugsDbClient bugsDbClient,
    ICommentsDbClient commentsDbClient,
    IAttachmentDbClient attachmentDbClient,
    IFileStorageClient fileStorageClient,
    AttachmentService attachmentService)
{
    public Task<AttachmentDbModel?> GetAttachmentForDownloadAsync(int attachmentId)
        => attachmentDbClient.GetByIdAsync(attachmentId);

    public Task<Stream> OpenContentStreamAsync(AttachmentDbModel attachmentDbModel)
        => fileStorageClient.ReadAsync(attachmentDbModel.StorageKey!);

    public async Task<MonadeStruct<AttachmentDbModel>> UploadAsync(
        int bugId,
        AttachType attachType,
        Stream fileStream,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        var locator = await bugsDbClient.GetBugLocatorAsync(bugId);
        if (locator is null)
        {
            return BoErrors.BugNotFoundError;
        }

        return await attachmentService.SaveBugAttachmentInternalAsync(
            creatorUserId: locator.CreatorUserId,
            reportId: locator.ReportId,
            publicId: locator.PublicId,
            teamReportId: locator.TeamReportId,
            creatorTeamId: locator.CreatorTeamId,
            creatorOrganizationId: locator.CreatorOrganizationId,
            bugId: bugId,
            fileStream: fileStream,
            attachType: attachType,
            fileMeta: fileMeta,
            ct: ct);
    }

    public async Task<MonadeStruct<AttachmentDbModel>> UploadBugStepAttachmentAsync(
        int bugId,
        int stepId,
        Stream fileStream,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        var locator = await bugsDbClient.GetBugLocatorAsync(bugId);
        if (locator is null)
        {
            return BoErrors.BugNotFoundError;
        }

        return await attachmentService.SaveBugStepAttachmentInternalAsync(
            creatorUserId: locator.CreatorUserId,
            reportId: locator.ReportId,
            publicId: locator.PublicId,
            teamReportId: locator.TeamReportId,
            creatorTeamId: locator.CreatorTeamId,
            creatorOrganizationId: locator.CreatorOrganizationId,
            bugId: bugId,
            stepId: stepId,
            fileStream: fileStream,
            fileMeta: fileMeta,
            ct: ct);
    }

    public async Task<MonadeStruct<AttachmentDbModel>> UploadCommentAttachmentAsync(
        int commentId,
        Stream fileStream,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        var locator = await commentsDbClient.GetCommentLocatorAsync(commentId);
        if (locator is null)
        {
            return BoErrors.CommentNotFoundError;
        }

        return await attachmentService.SaveCommentAttachmentInternalAsync(
            creatorUserId: locator.CreatorUserId,
            reportId: locator.ReportId,
            publicId: locator.PublicId,
            teamReportId: locator.TeamReportId,
            creatorTeamId: locator.CreatorTeamId,
            creatorOrganizationId: locator.CreatorOrganizationId,
            bugId: locator.BugId,
            commentId: commentId,
            fileStream: fileStream,
            fileMeta: fileMeta,
            ct: ct);
    }
}
