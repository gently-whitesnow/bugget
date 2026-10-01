using Bugget.Domain;
using Bugget.Domain.Authentication;

namespace Bugget.Application.Services.Attachments;

public sealed record AttachmentBatchTarget(UserIdentity User, int ReportId, AttachType AttachType);
