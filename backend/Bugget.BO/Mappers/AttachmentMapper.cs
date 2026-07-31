using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.SocketViews;
using Bugget.Entities.Views.Attachment;

namespace Bugget.BO.Mappers;

public static class AttachmentMapper
{
    public static AttachmentSocketView ToSocketView(this Attachment attachmentDbModel)
    {
        return new AttachmentSocketView
        {
            Id = attachmentDbModel.Id,
            EntityId = attachmentDbModel.EntityId,
            AttachType = attachmentDbModel.AttachType,
            CreatedAt = attachmentDbModel.CreatedAt,
            CreatorUserId = attachmentDbModel.CreatorUserId,
            FileName = attachmentDbModel.FileName,
            HasPreview = attachmentDbModel.HasPreview == true,
        };
    }

    public static AttachmentView ToView(this Attachment attachmentDbModel)
    {
        return new AttachmentView
        {
            Id = attachmentDbModel.Id,
            EntityId = attachmentDbModel.EntityId,
            AttachType = attachmentDbModel.AttachType,
            CreatedAt = attachmentDbModel.CreatedAt,
            CreatorUserId = attachmentDbModel.CreatorUserId,
            FileName = attachmentDbModel.FileName,
            HasPreview = attachmentDbModel.HasPreview == true,
        };
    }
}
