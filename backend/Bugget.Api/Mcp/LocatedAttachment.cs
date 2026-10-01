using System.IO.Compression;
using System.Text;
using Bugget.Application.Services.Attachments;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Constants;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Bugget.Api.Mcp;

/// <summary>
/// Вложение вместе с координатами родителя в дереве репорта: сервисные методы
/// чтения требуют bugId и идентификатор комментария либо шага.
/// </summary>
/// <param name="Attachment">Само вложение из дерева репорта.</param>
/// <param name="BugId">Баг, в поддереве которого нашлось вложение.</param>
/// <param name="ParentId">Комментарий или шаг; для вложения самого бага не используется.</param>
internal sealed record LocatedAttachment(Attachment Attachment, int BugId, int ParentId);
