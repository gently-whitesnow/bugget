using Bugget.BO.Ports;
using Bugget.Entities.BO.AttachmentBo;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class AttachmentDbClient : PostgresClient, IAttachmentDbClient
{
    public async Task<Attachment[]> DeleteCommentAttachmentsAsync(int commentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return (await connection.QueryAsync<Attachment>(
            "SELECT * FROM public.delete_comment_attachments_internal(@commentId)",
            new
            {
                commentId
            }
        )).ToArray();
    }

    public async Task<Attachment> UpdateAttachmentAsync(AttachmentUpdate update)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<Attachment>(
            "SELECT * FROM public.update_attachment_internal(@id, @storage_key, @storage_kind, @length_bytes, @file_name, @mime_type, @has_preview, @is_gzip_compressed)",
            new
            {
                id = update.Id,
                storage_key = update.StorageKey,
                storage_kind = update.StorageKind,
                length_bytes = update.LengthBytes,
                file_name = update.FileName,
                mime_type = update.MimeType,
                has_preview = update.HasPreview,
                is_gzip_compressed = update.IsGzipCompressed
            }
        );
    }

    /// <summary>
    /// Резолв attachment'а по голому id — для internal-контекста (beta-bot скачивает
    /// файл по `bugget.attachment.created` событию). ACL уже отработан на уровне сети
    /// через `X-Client-Name: beta-bot`. См. TECHSPEC §4.3.3.
    /// </summary>
    public async Task<Attachment?> GetByIdAsync(int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
            @"SELECT id, attach_type, entity_id, storage_key, storage_kind,
                     creator_user_id, length_bytes, file_name, mime_type,
                     has_preview, is_gzip_compressed, created_at
              FROM public.attachments
              WHERE id = @attachmentId",
            new { attachmentId }
        );
    }

    public async Task<Attachment?> GetBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
            "SELECT * FROM public.get_bug_attachment_internal(@reportId, @bugId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                attachmentId
            }
        );
    }

    public async Task<Attachment?> GetCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
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

    public async Task<Attachment?> GetBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
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

    public async Task<Attachment> CreateAttachment(AttachmentCreate create)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<Attachment>(
            "SELECT * FROM public.create_attachment_internal(@entity_id, @attach_type, @storage_key, @storage_kind, @creator_user_id, @length_bytes, @file_name, @mime_type)",
            new
            {
                entity_id = create.EntityId,
                attach_type = create.AttachType,
                storage_key = create.StorageKey,
                storage_kind = create.StorageKind,
                creator_user_id = create.CreatorUserId,
                length_bytes = create.LengthBytes,
                file_name = create.FileName,
                mime_type = create.MimeType,
            }
        );
    }

    public async Task<Attachment?> DeleteBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
            "SELECT * FROM public.delete_bug_attachment_internal(@reportId, @bugId, @attachmentId)",
            new
            {
                reportId,
                bugId,
                attachmentId
            }
        );
    }

    public async Task<Attachment?> DeleteCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
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

    public async Task<Attachment?> DeleteBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<Attachment>(
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

    public async Task<Attachment[]> DeleteBugStepAttachmentsAsync(int stepId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return (await connection.QueryAsync<Attachment>(
            "SELECT * FROM public.delete_bug_step_attachments_internal(@stepId)",
            new
            {
                stepId
            }
        )).ToArray();
    }
}
