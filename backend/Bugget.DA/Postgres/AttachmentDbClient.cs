using Bugget.DA.Interfaces;
using Bugget.Entities.DbModels.Attachment;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class AttachmentDbClient : PostgresClient, IAttachmentDbClient
{
    public async Task<AttachmentDbModel[]> DeleteCommentAttachmentsAsync(int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return (await connection.QueryAsync<AttachmentDbModel>(
            "SELECT * FROM public.delete_comment_attachments_internal(@commentId)",
            new
            {
                commentId
            }
        )).ToArray();
    }

    public async Task<AttachmentDbModel> UpdateAttachmentAsync(UpdateAttachmentDbModel updateAttachmentDbModel)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<AttachmentDbModel>(
            "SELECT * FROM public.update_attachment_internal(@id, @storage_key, @storage_kind, @length_bytes, @file_name, @mime_type, @has_preview, @is_gzip_compressed)",
            new
            {
                id = updateAttachmentDbModel.Id,
                storage_key = updateAttachmentDbModel.StorageKey,
                storage_kind = updateAttachmentDbModel.StorageKind,
                length_bytes = updateAttachmentDbModel.LengthBytes,
                file_name = updateAttachmentDbModel.FileName,
                mime_type = updateAttachmentDbModel.MimeType,
                has_preview = updateAttachmentDbModel.HasPreview,
                is_gzip_compressed = updateAttachmentDbModel.IsGzipCompressed
            }
        );
    }

    /// <summary>
    /// Резолв attachment'а по голому id — для internal-контекста (beta-bot скачивает
    /// файл по `bugget.attachment.created` событию). ACL уже отработан на уровне сети
    /// через `X-Client-Name: beta-bot`. См. TECHSPEC §4.3.3.
    /// </summary>
    public async Task<AttachmentDbModel?> GetByIdAsync(int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            @"SELECT id, attach_type, entity_id, storage_key, storage_kind,
                     creator_user_id, length_bytes, file_name, mime_type,
                     has_preview, is_gzip_compressed, created_at
              FROM public.attachments
              WHERE id = @attachmentId",
            new { attachmentId }
        );
    }

    public async Task<AttachmentDbModel?> GetBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            "SELECT * FROM public.get_bug_attachment_internal(@reportId, @bugId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                attachmentId
            }
        );
    }

    public async Task<AttachmentDbModel?> GetCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            "SELECT * FROM public.get_comment_attachment_internal(@reportId, @bugId, @commentId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                commentId,
                attachmentId
            }
        );
    }

    public async Task<AttachmentDbModel?> GetBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            "SELECT * FROM public.get_bug_step_attachment_internal(@reportId, @bugId, @stepId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                stepId,
                attachmentId
            }
        );
    }

    public async Task<int> GetBugAttachmentsCountInternalAsync(int reportId, int bugId, int attachType)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT public.get_bug_attachments_count_internal(@reportId, @bugId, @attachType)",
            new
            {
                reportId,
                bugId,
                attachType
            }
        );

        return result;
    }

    public async Task<int> GetCommentAttachmentsCountInternalAsync(string userId, int reportId, int bugId, int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT public.get_comment_attachments_count_internal(@userId, @reportId, @bugId, @commentId)",
            new
            {
                userId,
                reportId,
                bugId,
                commentId
            }
        );

        return result;
    }

    public async Task<int> GetBugStepAttachmentsCountInternalAsync(int reportId, int bugId, int stepId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT public.get_bug_step_attachments_count_internal(@reportId, @bugId, @stepId)",
            new
            {
                reportId,
                bugId,
                stepId
            }
        );

        return result;
    }

    public async Task<AttachmentDbModel> CreateAttachment(CreateAttachmentDbModel attachmentCreateDbModel)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<AttachmentDbModel>(
            "SELECT * FROM public.create_attachment_internal(@entity_id, @attach_type, @storage_key, @storage_kind, @creator_user_id, @length_bytes, @file_name, @mime_type)",
            new
            {
                entity_id = attachmentCreateDbModel.EntityId,
                attach_type = attachmentCreateDbModel.AttachType,
                storage_key = attachmentCreateDbModel.StorageKey,
                storage_kind = attachmentCreateDbModel.StorageKind,
                creator_user_id = attachmentCreateDbModel.CreatorUserId,
                length_bytes = attachmentCreateDbModel.LengthBytes,
                file_name = attachmentCreateDbModel.FileName,
                mime_type = attachmentCreateDbModel.MimeType,
            }
        );
    }

    public async Task<AttachmentDbModel?> DeleteBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            "SELECT * FROM public.delete_bug_attachment_internal(@reportId, @bugId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                attachmentId
            }
        );
    }

    public async Task<AttachmentDbModel?> DeleteCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            "SELECT * FROM public.delete_comment_attachment_internal(@reportId, @bugId, @commentId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                commentId,
                attachmentId
            }
        );
    }

    public async Task<AttachmentDbModel?> DeleteBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<AttachmentDbModel>(
            "SELECT * FROM public.delete_bug_step_attachment_internal(@reportId, @bugId, @stepId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                stepId,
                attachmentId
            }
        );
    }

    public async Task<AttachmentDbModel[]> DeleteBugStepAttachmentsAsync(int stepId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return (await connection.QueryAsync<AttachmentDbModel>(
            "SELECT * FROM public.delete_bug_step_attachments_internal(@stepId)",
            new
            {
                stepId
            }
        )).ToArray();
    }
}
