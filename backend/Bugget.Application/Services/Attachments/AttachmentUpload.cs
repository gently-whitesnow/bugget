using Bugget.Domain.Attachments;

namespace Bugget.Application.Services.Attachments;

public sealed record AttachmentUpload(Stream Content, FileMeta Meta);
