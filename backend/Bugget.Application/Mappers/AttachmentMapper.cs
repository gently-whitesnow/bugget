using Bugget.Contracts.SocketViews;
using Bugget.Contracts.Views.Attachment;
using Bugget.Domain.Attachments;

namespace Bugget.Application.Mappers;

public static class AttachmentMapper
{
    public static AttachmentSocketView ToSocketView(this Attachment attachment)
    {
        return new AttachmentSocketView
        {
            Id = attachment.Id,
            EntityId = attachment.EntityId,
            AttachType = attachment.AttachType,
            CreatedAt = attachment.CreatedAt,
            CreatorUserId = attachment.CreatorUserId,
            FileName = attachment.FileName,
            HasPreview = attachment.HasPreview == true,
        };
    }

    public static AttachmentView ToView(this Attachment attachment)
    {
        return new AttachmentView
        {
            Id = attachment.Id,
            EntityId = attachment.EntityId,
            AttachType = attachment.AttachType,
            CreatedAt = attachment.CreatedAt,
            CreatorUserId = attachment.CreatorUserId,
            FileName = attachment.FileName,
            HasPreview = attachment.HasPreview == true,
        };
    }
}
