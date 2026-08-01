using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services.Attachments;

public interface IAttachmentService
{
    Task<((Stream Content, Attachment Model)? Value, Error? Error)> GetBugAttachmentContentAsync(UserIdentity user, string aliasId, int bugId, int attachmentId);
    Task<((Stream Content, Attachment Model)? Value, Error? Error)> GetCommentAttachmentContentAsync(UserIdentity user, string aliasId, int bugId, int commentId, int attachmentId);
    Task<((Stream Content, Attachment Model)? Value, Error? Error)> GetBugStepAttachmentContentAsync(UserIdentity user, string aliasId, int bugId, int stepId, int attachmentId);
    Task<(Stream? Value, Error? Error)> GetBugAttachmentPreviewContentAsync(UserIdentity user, string aliasId, int bugId, int attachmentId);
    Task<(Stream? Value, Error? Error)> GetCommentAttachmentPreviewContentAsync(UserIdentity user, string aliasId, int bugId, int commentId, int attachmentId);
    Task<(Stream? Value, Error? Error)> GetBugStepAttachmentPreviewContentAsync(UserIdentity user, string aliasId, int bugId, int stepId, int attachmentId);
    Task<Error?> DeleteBugAttachmentAsync(UserIdentity user, string aliasId, int bugId, int attachmentId);
    Task<(Attachment? Value, Error? Error)> RenameBugAttachmentAsync(UserIdentity user, string aliasId, int bugId, int attachmentId, string fileName);
    Task<(Attachment? Value, Error? Error)> RenameBugStepAttachmentAsync(UserIdentity user, string aliasId, int bugId, int stepId, int attachmentId, string fileName);
    Task<(Attachment? Value, Error? Error)> RenameCommentAttachmentAsync(UserIdentity user, string aliasId, int bugId, int commentId, int attachmentId, string fileName);
    Task<Error?> DeleteBugStepAttachmentAsync(UserIdentity user, string aliasId, int bugId, int stepId, int attachmentId);
    Task<Error?> DeleteCommentAttachmentAsync(UserIdentity user, string aliasId, int bugId, int commentId, int attachmentId);
    Task DeleteCommentAttachmentsInternalAsync(int commentId);
    Task DeleteBugStepAttachmentsInternalAsync(int stepId);
    Task<(Attachment? Value, Error? Error)> SaveBugAttachmentAsync(UserIdentity user, string aliasId, int bugId, Stream fileStream, AttachType attachType, FileMeta fileMeta, CancellationToken ct);
    Task<(Attachment? Value, Error? Error)> SaveBugStepAttachmentAsync(UserIdentity user, string aliasId, int bugId, int stepId, Stream fileStream, FileMeta fileMeta, CancellationToken ct);
    Task<(Attachment? Value, Error? Error)> SaveCommentAttachmentAsync(UserIdentity user, string aliasId, int bugId, int commentId, Stream fileStream, FileMeta fileMeta, CancellationToken ct);
}
