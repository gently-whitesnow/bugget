using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.SocketViews;
using Bugget.Entities.Views.Attachment;

namespace Bugget.BO.Mappers;

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
