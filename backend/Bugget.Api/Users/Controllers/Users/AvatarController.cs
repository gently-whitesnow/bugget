using Bugget.Api.Generated.Users;
using Bugget.Api.Http;
using Bugget.Api.Users.Authentication;
using Bugget.Application.Ports;
using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Ports;
using Microsoft.AspNetCore.Mvc;
using FileParameter = Bugget.Api.Generated.Users.FileParameter;
using HttpProblemDetailsFactory = Bugget.Api.Http.ProblemDetailsFactory;
using UsersModule = Bugget.Api.Users.Extensions.ServiceCollectionExtensions;

namespace Bugget.Api.Users.Controllers.Users;

/// <summary>Аватары пользователей; маршруты и формы — из <c>specs/contracts/users/openapi.yaml</c>.</summary>
/// <remarks>Путь несёт контекст workspace и команды (по нему ходит фронт), но пользователь берётся из identity.</remarks>
[ApiController]
[Auth]
public sealed class AvatarController(
    IUsersService userService,
    IAvatarDownloadService avatarService,
    [FromKeyedServices(UsersModule.FileStorageServiceKey)] IFileStorageClient fileStorageClient) : AvatarControllerBase
{
    private const long MaxAvatarSize = 200 * 1024;
    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };
    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    public override async Task<IActionResult> DeleteAvatarInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await avatarService.DeleteAvatarAsync(user.Id, cancellationToken);
        return NoContent();
    }

    public override async Task<IActionResult> UploadAvatarInContext(
        string workspaceId,
        string teamId,
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default)
    {
        var content = file.Data;
        if (content.Length > MaxAvatarSize)
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.AvatarTooLarge);
        }

        if (!AllowedAvatarContentTypes.Contains(file.ContentType))
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.AvatarFormatNotAllowed);
        }

        var user = User.GetIdentity();
        await using var stream = content;
        await avatarService.UploadAvatarAsync(user.Id, stream, file.ContentType, cancellationToken);
        return Ok();
    }

    public override async Task<IActionResult> GetAvatarContentInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();
        var user = await userService.GetUserAsync(identity.Id);
        if (user?.ImageUrl is null)
        {
            return NotFound();
        }

        return await StreamAvatarAsync(user.ImageUrl, cancellationToken);
    }

    /// <summary>Получить аватар пользователя из текущего workspace.</summary>
    /// <remarks>
    /// Сегмент — строка канонического Int64, внутрь уходит <c>long</c>. Ограничение <c>:long</c> оставлено ради 404
    /// на нечисловой сегмент; неканоничный (<c>-5</c>, <c>007</c>) отбивает <see cref="WireInt64"/> с 400.
    /// </remarks>
    [WorkspaceRequired]
    [RouteParameterConstraint("userId", "long")]
    public override async Task<IActionResult> GetUserAvatarContentInContext(
        string workspaceId,
        string teamId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();
        if (identity.WorkspaceId is null)
        {
            return NotFound();
        }

        var invalidUserId = WireInt64.TryBindRouteValue(HttpContext, "userId", userId, out var id);
        if (invalidUserId is not null)
        {
            return invalidUserId;
        }

        var users = await userService.ListUsersAsync([id], identity.WorkspaceId);
        var user = users.FirstOrDefault();
        if (user?.ImageUrl is null)
        {
            return NotFound();
        }

        return await StreamAvatarAsync(user.ImageUrl, cancellationToken);
    }

    private async Task<IActionResult> StreamAvatarAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            var content = await fileStorageClient.ReadAsync(storageKey, ct);
            return new FileStreamResult(content, GetContentType(storageKey));
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private static string GetContentType(string storageKey)
    {
        var extension = Path.GetExtension(storageKey);
        return ContentTypeByExtension.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
