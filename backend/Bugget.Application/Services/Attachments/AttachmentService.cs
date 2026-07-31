using Bugget.Application.Errors;
using Bugget.Application.Interfaces;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Bugs;
using Bugget.Application.Services.Comments;
using Bugget.Application.Services.Reports;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Services.Attachments;

public sealed class AttachmentService(
    IAttachmentDbClient attachmentDbClient,
    ITaskQueue taskQueue,
    AttachmentEventsService attachmentEventsService,
    LimitsService limitsService,
    ILogger<AttachmentService> logger,
    IFileStorageClient fileStorageClient,
    IAttachmentKeyGenerator keyGen,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions)
{
    private Task<ResolvedReportId?> ResolveReportAsync(UserIdentity user, string aliasId)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        return reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
    }

    public async Task<((Stream Content, Attachment Model)? Value, Error? Error)> GetBugAttachmentContentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachment?.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var content = await fileStorageClient.ReadAsync(attachment.StorageKey);
        return ((content, attachment), null);
    }

    public async Task<((Stream Content, Attachment Model)? Value, Error? Error)> GetCommentAttachmentContentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int commentId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachment?.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var content = await fileStorageClient.ReadAsync(attachment.StorageKey);
        return ((content, attachment), null);
    }

    public async Task<((Stream Content, Attachment Model)? Value, Error? Error)> GetBugStepAttachmentContentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int stepId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachment?.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var content = await fileStorageClient.ReadAsync(attachment.StorageKey);
        return ((content, attachment), null);
    }

    public async Task<(Stream? Value, Error? Error)> GetBugAttachmentPreviewContentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachment?.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var content = await fileStorageClient.ReadAsync(keyGen.GetPreviewKey(attachment.StorageKey));
        return (content, null);
    }

    public async Task<(Stream? Value, Error? Error)> GetCommentAttachmentPreviewContentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int commentId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachment?.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var content = await fileStorageClient.ReadAsync(keyGen.GetPreviewKey(attachment.StorageKey));
        return (content, null);
    }

    public async Task<(Stream? Value, Error? Error)> GetBugStepAttachmentPreviewContentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int stepId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachment?.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var content = await fileStorageClient.ReadAsync(keyGen.GetPreviewKey(attachment.StorageKey));
        return (content, null);
    }

    public async Task<Error?> DeleteBugAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var attachment = await attachmentDbClient.DeleteBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachment == null)
        {
            return null;
        }


        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentDeleteEventAsync(reportIdContext, user, attachment));

        return null;
    }

    public async Task<(Attachment? Value, Error? Error)> RenameBugAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int attachmentId,
        string fileName)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachment == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        return await RenameAsync(
            new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            attachment,
            fileName);
    }

    public async Task<(Attachment? Value, Error? Error)> RenameBugStepAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int stepId,
        int attachmentId,
        string fileName)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachment == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        return await RenameAsync(
            new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            attachment,
            fileName);
    }

    public async Task<(Attachment? Value, Error? Error)> RenameCommentAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int commentId,
        int attachmentId,
        string fileName)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var attachment = await attachmentDbClient.GetCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachment == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        return await RenameAsync(
            new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            attachment,
            fileName);
    }

    public async Task<Error?> DeleteBugStepAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int stepId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var attachment = await attachmentDbClient.DeleteBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachment == null)
        {
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentDeleteEventAsync(reportIdContext, user, attachment));

        return null;
    }

    public async Task<Error?> DeleteCommentAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int commentId,
        int attachmentId)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var attachment = await attachmentDbClient.DeleteCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachment == null)
        {
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentDeleteEventAsync(reportIdContext, user, attachment));

        return null;
    }

    public async Task DeleteCommentAttachmentsInternalAsync(
        int commentId)
    {
        var attachments = await attachmentDbClient.DeleteCommentAttachmentsAsync(commentId);
        if (attachments.Length == 0)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            if (attachment.StorageKey is null)
            {
                continue;
            }
            await fileStorageClient.DeleteAsync(attachment.StorageKey);
            if (attachment.HasPreview == true)
            {
                await fileStorageClient.DeleteAsync(keyGen.GetPreviewKey(attachment.StorageKey));
            }
        }
    }

    public async Task DeleteBugStepAttachmentsInternalAsync(
        int stepId)
    {
        var attachments = await attachmentDbClient.DeleteBugStepAttachmentsAsync(stepId);
        if (attachments.Length == 0)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            if (attachment.StorageKey is null)
            {
                continue;
            }
            await fileStorageClient.DeleteAsync(attachment.StorageKey);
            if (attachment.HasPreview == true)
            {
                await fileStorageClient.DeleteAsync(keyGen.GetPreviewKey(attachment.StorageKey));
            }
        }
    }

    public async Task<(Attachment? Value, Error? Error)> SaveBugAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        Stream fileStream,
        AttachType attachType,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        Task<Error?> validateLimit() => limitsService.ValidateBugAttachmentLimitAsync(resolvedReport.Id, bugId, attachType);

        return await SaveAsync(
            user: user,
            reportIdContext: new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            entityId: bugId,
            validateLimit: validateLimit,
            fileStream: fileStream,
            attachType: attachType,
            fileMeta: fileMeta,
            ct: ct);
    }

    public async Task<(Attachment? Value, Error? Error)> SaveBugStepAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int stepId,
        Stream fileStream,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        Task<Error?> validateLimit() => limitsService.ValidateBugStepAttachmentLimitAsync(resolvedReport.Id, bugId, stepId);

        return await SaveAsync(
            user: user,
            reportIdContext: new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            entityId: stepId,
            validateLimit: validateLimit,
            fileStream: fileStream,
            attachType: AttachType.BugStep,
            fileMeta: fileMeta,
            ct: ct);
    }

    public async Task<(Attachment? Value, Error? Error)> SaveCommentAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int commentId,
        Stream fileStream,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        Task<Error?> validateLimit() => limitsService.ValidateCommentAttachmentLimitAsync(user.Id, resolvedReport.Id, bugId, commentId);

        return await SaveAsync(
            user: user,
            reportIdContext: new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            entityId: commentId,
            validateLimit: validateLimit,
            fileStream: fileStream,
            attachType: AttachType.Comment,
            fileMeta: fileMeta,
            ct: ct);
    }

    private async Task<(Attachment? Value, Error? Error)> SaveAsync(
        UserIdentity user,
        ReportIdContext reportIdContext,
        int entityId,
        Func<Task<Error?>> validateLimit,
        Stream fileStream,
        AttachType attachType,
        FileMeta fileMeta,
        CancellationToken ct)
    {
        // 1) Общая валидация по метаданным
        var validationError = AttachmentValidator.Validate(fileMeta);
        if (validationError != null)
        {
            return (null, validationError);
        }

        // 2) Лимиты 
        var limitError = await validateLimit();
        if (limitError != null)
        {
            return (null, limitError);
        }

        var canOptimize = AttachmentOptimizator.CanOptimize(fileMeta.TrustedMimeType);

        // 3) Сохраняем как есть во временный путь
        var storageKey = canOptimize ?
        keyGen.GetTempKey(user.OrganizationId, reportIdContext.ReportId, entityId, Path.GetExtension(fileMeta.FileName).ToLowerInvariant())
        : keyGen.GetOriginalKey(user.OrganizationId, reportIdContext.ReportId, entityId, Path.GetExtension(fileMeta.FileName).ToLowerInvariant());
        await fileStorageClient.WriteAsync(storageKey, fileStream, ct);

        logger.LogInformation("Attachment saved: {@FileName} to {tmpPath}",
        fileMeta.FileName, storageKey);

        // 4) Формируем модель для БД
        var createModel = new AttachmentCreate
        {
            EntityId = entityId,
            AttachType = (int)attachType,
            StorageKey = storageKey,
            StorageKind = canOptimize ? (int)StorageKind.Temp : (int)StorageKind.Standard,
            CreatorUserId = user.Id,
            FileName = fileMeta.FileName,
            MimeType = fileMeta.TrustedMimeType,
            LengthBytes = fileStream.Length
        };

        // 5) Сохраняем в БД
        var dbModel = await attachmentDbClient.CreateAttachment(createModel);

        // 6) Событие
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentCreateEventAsync(reportIdContext, user, dbModel));

        return (dbModel, null);
    }

    private async Task<(Attachment? Value, Error? Error)> RenameAsync(
        ReportIdContext reportIdContext,
        Attachment attachment,
        string fileName)
    {
        var normalizedFileName = fileName?.Trim() ?? "";
        var validationError = AttachmentValidator.ValidateFileName(normalizedFileName);
        if (validationError != null)
        {
            return (null, validationError);
        }

        if (attachment.StorageKey == null)
        {
            return (null, BoErrors.AttachmentNotFound);
        }

        var updatedAttachment = await attachmentDbClient.UpdateAttachmentAsync(new AttachmentUpdate
        {
            Id = attachment.Id,
            StorageKey = attachment.StorageKey,
            StorageKind = attachment.StorageKind ?? 0,
            LengthBytes = attachment.LengthBytes ?? 0,
            FileName = normalizedFileName,
            MimeType = attachment.MimeType,
            HasPreview = attachment.HasPreview == true,
            IsGzipCompressed = attachment.IsGzipCompressed == true,
        });

        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentRenameEventAsync(reportIdContext, updatedAttachment));

        return (updatedAttachment, null);
    }
}
