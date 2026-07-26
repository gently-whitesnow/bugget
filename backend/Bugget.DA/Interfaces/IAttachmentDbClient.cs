using Bugget.Entities.DbModels.Attachment;

namespace Bugget.DA.Interfaces;

public interface IAttachmentDbClient
{
    Task<AttachmentDbModel[]> DeleteCommentAttachmentsAsync(int commentId);

    Task<AttachmentDbModel> UpdateAttachmentAsync(UpdateAttachmentDbModel updateAttachmentDbModel);

    Task<AttachmentDbModel?> GetByIdAsync(int attachmentId);

    Task<AttachmentDbModel?> GetBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId);

    Task<AttachmentDbModel?> GetCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId);

    Task<AttachmentDbModel?> GetBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId);

    Task<int> GetBugAttachmentsCountInternalAsync(int reportId, int bugId, int attachType);

    Task<int> GetCommentAttachmentsCountInternalAsync(string userId, int reportId, int bugId, int commentId);

    Task<int> GetBugStepAttachmentsCountInternalAsync(int reportId, int bugId, int stepId);

    Task<AttachmentDbModel> CreateAttachment(CreateAttachmentDbModel attachmentCreateDbModel);

    Task<AttachmentDbModel?> DeleteBugAttachmentInternalAsync(int reportId, int bugId, int attachmentId);

    Task<AttachmentDbModel?> DeleteCommentAttachmentInternalAsync(int reportId, int bugId, int commentId, int attachmentId);

    Task<AttachmentDbModel?> DeleteBugStepAttachmentInternalAsync(int reportId, int bugId, int stepId, int attachmentId);

    Task<AttachmentDbModel[]> DeleteBugStepAttachmentsAsync(int stepId);
}
