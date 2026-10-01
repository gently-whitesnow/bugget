using Bugget.Application.Errors;
using Bugget.Application.Interfaces;
using Bugget.Application.Ports;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Bugget.Application.Services.Attachments;

/// <summary>
/// Создаёт запись-владельца и все её вложения одной транзакцией (ADR-0015).
/// Файл в хранилище транзакцией не откатывается, поэтому записанные ключи
/// удаляются вручную при любом сбое, включая сбой коммита.
/// </summary>
public sealed class AttachmentBatchWriter(
    IAttachmentDbClient attachmentDbClient,
    IFileStorageClient fileStorageClient,
    IAttachmentKeyGenerator keyGen,
    IUnitOfWork unitOfWork,
    AttachmentEventsService attachmentEventsService,
    ILogger<AttachmentBatchWriter> logger)
{
    public static Error? Validate(IReadOnlyList<AttachmentUpload> files)
    {
        if (files.Count == 0)
        {
            return BoErrors.AttachmentFileNotSelectedOrEmpty;
        }

        if (files.Count > LimitsService.MaxAttachmentsCount)
        {
            return BoErrors.AttachmentLimitExceeded;
        }

        return files.Select(file => AttachmentValidator.Validate(file.Meta)).FirstOrDefault(error => error != null);
    }

    public async Task<(TOwner Owner, Attachment[] Attachments)> CreateAsync<TOwner>(
        AttachmentBatchTarget target,
        IReadOnlyList<AttachmentUpload> files,
        Func<ITransactionScope, CancellationToken, Task<(TOwner Owner, int EntityId)>> createOwner,
        CancellationToken ct)
    {
        var writtenKeys = new List<string>(files.Count);
        try
        {
            return await unitOfWork.ExecuteAsync(async (scope, token) =>
            {
                var (owner, entityId) = await createOwner(scope, token);
                var attachments = new Attachment[files.Count];
                for (var i = 0; i < files.Count; i++)
                {
                    attachments[i] = await WriteAsync(scope, target, entityId, files[i], writtenKeys, token);
                }

                return (owner, attachments);
            }, ct);
        }
        catch
        {
            await DiscardAsync(writtenKeys);
            throw;
        }
    }

    /// <summary>Вызывается после коммита: до него события о вложениях уходить не должны.</summary>
    public Task PublishCreatedAsync(
        ReportIdContext reportIdContext,
        UserIdentity user,
        IReadOnlyList<Attachment> attachments,
        CancellationToken ct) =>
        attachmentEventsService.HandleAttachmentsCreateEventAsync(reportIdContext, user, attachments, ct);

    private async Task<Attachment> WriteAsync(
        ITransactionScope scope,
        AttachmentBatchTarget target,
        int entityId,
        AttachmentUpload file,
        List<string> writtenKeys,
        CancellationToken ct)
    {
        var canOptimize = AttachmentOptimizator.CanOptimize(file.Meta.TrustedMimeType);
        var extension = Path.GetExtension(file.Meta.FileName).ToLowerInvariant();
        var organizationId = target.User.OrganizationId;
        var storageKey = canOptimize
            ? keyGen.GetTempKey(organizationId, target.ReportId, entityId, extension)
            : keyGen.GetOriginalKey(organizationId, target.ReportId, entityId, extension);

        writtenKeys.Add(storageKey);
        await fileStorageClient.WriteAsync(storageKey, file.Content, ct);

        return await attachmentDbClient.CreateAttachmentAsync(scope, new AttachmentCreate
        {
            EntityId = entityId,
            AttachType = (int)target.AttachType,
            StorageKey = storageKey,
            StorageKind = canOptimize ? (int)StorageKind.Temp : (int)StorageKind.Standard,
            CreatorUserId = target.User.Id,
            FileName = file.Meta.FileName,
            MimeType = file.Meta.TrustedMimeType,
            LengthBytes = file.Content.Length,
        });
    }

    private async Task DiscardAsync(List<string> writtenKeys)
    {
        foreach (var key in writtenKeys)
        {
            try
            {
                await fileStorageClient.DeleteAsync(key);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Orphaned attachment file after rollback: {StorageKey}", key);
            }
        }
    }
}
