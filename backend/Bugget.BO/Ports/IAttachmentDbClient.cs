using Bugget.Entities.BO.AttachmentBo;

namespace Bugget.BO.Ports;

public interface IAttachmentDbClient
{
    Task<Attachment[]> DeleteCommentAttachmentsAsync(int commentId);

    Task<Attachment> UpdateAttachmentAsync(AttachmentUpdate update);

    Task<Attachment?> GetByIdAsync(int attachmentId);

    Task<Attachment?> GetBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId);

    Task<Attachment?> GetCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId);

    Task<Attachment?> GetBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId);

    Task<int> GetBugAttachmentsCountInternalAsync(int reportId, int bugId, int attachType);

    Task<int> GetCommentAttachmentsCountInternalAsync(string userId, int reportId, int bugId, int commentId);

    Task<int> GetBugStepAttachmentsCountInternalAsync(int reportId, int bugId, int stepId);

    Task<Attachment> CreateAttachment(AttachmentCreate create);

    Task<Attachment?> DeleteBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId);

    Task<Attachment?> DeleteCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId);

    Task<Attachment?> DeleteBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId);

    Task<Attachment[]> DeleteBugStepAttachmentsAsync(int stepId);
}
