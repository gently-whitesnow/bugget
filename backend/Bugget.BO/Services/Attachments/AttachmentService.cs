using Bugget.BO.Errors;
using Bugget.BO.Interfaces;
using Bugget.BO.Services.Bugs;
using Bugget.BO.Services.Comments;
using Bugget.BO.Services.Reports;
using Bugget.DA.Interfaces;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.Attachment;
using Bugget.Entities.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monade;
using TaskQueue;

namespace Bugget.BO.Services.Attachments;

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

    public async Task<MonadeStruct<(Stream content, AttachmentDbModel attachmentDbModel)>> GetBugAttachmentContentAsync(
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

        var attachmentDbModel = await attachmentDbClient.GetBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachmentDbModel?.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var content = await fileStorageClient.ReadAsync(attachmentDbModel.StorageKey);
        return (content, attachmentDbModel);
    }

    public async Task<MonadeStruct<(Stream content, AttachmentDbModel attachmentDbModel)>> GetCommentAttachmentContentAsync(
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

        var attachmentDbModel = await attachmentDbClient.GetCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachmentDbModel?.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var content = await fileStorageClient.ReadAsync(attachmentDbModel.StorageKey);
        return (content, attachmentDbModel);
    }

    public async Task<MonadeStruct<(Stream content, AttachmentDbModel attachmentDbModel)>> GetBugStepAttachmentContentAsync(
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

        var attachmentDbModel = await attachmentDbClient.GetBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachmentDbModel?.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var content = await fileStorageClient.ReadAsync(attachmentDbModel.StorageKey);
        return (content, attachmentDbModel);
    }

    public async Task<MonadeStruct<Stream>> GetBugAttachmentPreviewContentAsync(
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

        var attachmentDbModel = await attachmentDbClient.GetBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachmentDbModel?.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var content = await fileStorageClient.ReadAsync(keyGen.GetPreviewKey(attachmentDbModel.StorageKey));
        return content;
    }

    public async Task<MonadeStruct<Stream>> GetCommentAttachmentPreviewContentAsync(
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

        var attachmentDbModel = await attachmentDbClient.GetCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachmentDbModel?.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var content = await fileStorageClient.ReadAsync(keyGen.GetPreviewKey(attachmentDbModel.StorageKey));
        return content;
    }

    public async Task<MonadeStruct<Stream>> GetBugStepAttachmentPreviewContentAsync(
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

        var attachmentDbModel = await attachmentDbClient.GetBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachmentDbModel?.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var content = await fileStorageClient.ReadAsync(keyGen.GetPreviewKey(attachmentDbModel.StorageKey));
        return content;
    }

    public async Task<MonadeStruct> DeleteBugAttachmentAsync(
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

        var attachmentDbModel = await attachmentDbClient.DeleteBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachmentDbModel == null)
        {
            return MonadeStruct.Success;
        }


        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentDeleteEventAsync(reportIdContext, user, attachmentDbModel));

        return MonadeStruct.Success;
    }

    public async Task<MonadeStruct<AttachmentDbModel>> RenameBugAttachmentAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        int attachmentId,
        string fileName)
    {
        var resolvedReport = await ResolveReportAsync(user, aliasId);
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var attachmentDbModel = await attachmentDbClient.GetBugAttachmentInternalAsync(resolvedReport.Id, bugId, attachmentId);
        if (attachmentDbModel == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        return await RenameAsync(
            new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            attachmentDbModel,
            fileName);
    }

    public async Task<MonadeStruct<AttachmentDbModel>> RenameBugStepAttachmentAsync(
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
            return BoErrors.ReportNotFoundError;
        }

        var attachmentDbModel = await attachmentDbClient.GetBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachmentDbModel == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        return await RenameAsync(
            new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            attachmentDbModel,
            fileName);
    }

    public async Task<MonadeStruct<AttachmentDbModel>> RenameCommentAttachmentAsync(
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
            return BoErrors.ReportNotFoundError;
        }

        var attachmentDbModel = await attachmentDbClient.GetCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachmentDbModel == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        return await RenameAsync(
            new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId),
            attachmentDbModel,
            fileName);
    }

    public async Task<MonadeStruct> DeleteBugStepAttachmentAsync(
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

        var attachmentDbModel = await attachmentDbClient.DeleteBugStepAttachmentInternalAsync(resolvedReport.Id, bugId, stepId, attachmentId);
        if (attachmentDbModel == null)
        {
            return MonadeStruct.Success;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentDeleteEventAsync(reportIdContext, user, attachmentDbModel));

        return MonadeStruct.Success;
    }

    public async Task<MonadeStruct> DeleteCommentAttachmentAsync(
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

        var attachmentDbModel = await attachmentDbClient.DeleteCommentAttachmentInternalAsync(resolvedReport.Id, bugId, commentId, attachmentId);
        if (attachmentDbModel == null)
        {
            return MonadeStruct.Success;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentDeleteEventAsync(reportIdContext, user, attachmentDbModel));

        return MonadeStruct.Success;
    }

    public async Task DeleteCommentAttachmentsInternalAsync(
        int commentId)
    {
        var attachmentsDbModels = await attachmentDbClient.DeleteCommentAttachmentsAsync(commentId);
        if (attachmentsDbModels.Length == 0)
        {
            return;
        }

        foreach (var attachmentDbModel in attachmentsDbModels)
        {
            if (attachmentDbModel.StorageKey is null)
            {
                continue;
            }
            await fileStorageClient.DeleteAsync(attachmentDbModel.StorageKey);
            if (attachmentDbModel.HasPreview == true)
            {
                await fileStorageClient.DeleteAsync(keyGen.GetPreviewKey(attachmentDbModel.StorageKey));
            }
        }
    }

    public async Task DeleteBugStepAttachmentsInternalAsync(
        int stepId)
    {
        var attachmentsDbModels = await attachmentDbClient.DeleteBugStepAttachmentsAsync(stepId);
        if (attachmentsDbModels.Length == 0)
        {
            return;
        }

        foreach (var attachmentDbModel in attachmentsDbModels)
        {
            if (attachmentDbModel.StorageKey is null)
            {
                continue;
            }
            await fileStorageClient.DeleteAsync(attachmentDbModel.StorageKey);
            if (attachmentDbModel.HasPreview == true)
            {
                await fileStorageClient.DeleteAsync(keyGen.GetPreviewKey(attachmentDbModel.StorageKey));
            }
        }
    }

    public async Task<MonadeStruct<AttachmentDbModel>> SaveBugAttachmentAsync(
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
            return BoErrors.ReportNotFoundError;
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

    public async Task<MonadeStruct<AttachmentDbModel>> SaveBugStepAttachmentAsync(
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
            return BoErrors.ReportNotFoundError;
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

    public async Task<MonadeStruct<AttachmentDbModel>> SaveCommentAttachmentAsync(
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
            return BoErrors.ReportNotFoundError;
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

    private async Task<MonadeStruct<AttachmentDbModel>> SaveAsync(
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
            return validationError;
        }

        // 2) Лимиты 
        var limitError = await validateLimit();
        if (limitError != null)
        {
            return limitError;
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
        var createModel = new CreateAttachmentDbModel
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

        return dbModel;
    }

    private async Task<MonadeStruct<AttachmentDbModel>> RenameAsync(
        ReportIdContext reportIdContext,
        AttachmentDbModel attachmentDbModel,
        string fileName)
    {
        var normalizedFileName = fileName?.Trim() ?? "";
        var validationError = AttachmentValidator.ValidateFileName(normalizedFileName);
        if (validationError != null)
        {
            return validationError;
        }

        if (attachmentDbModel.StorageKey == null)
        {
            return BoErrors.AttachmentNotFound;
        }

        var updatedAttachment = await attachmentDbClient.UpdateAttachmentAsync(new UpdateAttachmentDbModel
        {
            Id = attachmentDbModel.Id,
            StorageKey = attachmentDbModel.StorageKey,
            StorageKind = attachmentDbModel.StorageKind ?? 0,
            LengthBytes = attachmentDbModel.LengthBytes ?? 0,
            FileName = normalizedFileName,
            MimeType = attachmentDbModel.MimeType,
            HasPreview = attachmentDbModel.HasPreview == true,
            IsGzipCompressed = attachmentDbModel.IsGzipCompressed == true,
        });

        await taskQueue.EnqueueAsync(async () =>
            await attachmentEventsService.HandleAttachmentRenameEventAsync(reportIdContext, updatedAttachment));

        return updatedAttachment;
    }
}
