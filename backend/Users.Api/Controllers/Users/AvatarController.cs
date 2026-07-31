using Authentication;
using Bugget.Http;
using Bugget.Http;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Generated;
using Users.BO.Interfaces;
using Users.DA.Interfaces;
using FileParameter = Users.Api.Generated.FileParameter;
using HttpProblemDetailsFactory = Bugget.Http.ProblemDetailsFactory;

namespace Users.Api.Controllers.Users;

/// <summary>
/// Аватары пользователей. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="AvatarControllerBase"/>.
/// </summary>
/// <remarks>
/// Как и профиль, все ручки живут по адресу с контекстом рабочего пространства
/// и команды: по нему ходит фронт. Идентификаторы из пути не используются —
/// пользователь берётся из identity.
/// </remarks>
[ApiController]
[Auth]
public sealed class AvatarController(
    IUsersService userService,
    IAvatarDownloadService avatarService,
    IFileStorageClient fileStorageClient) : AvatarControllerBase
{
    private const long MaxAvatarSize = 200 * 1024; // 200 KB
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

    /// <summary>
    /// Удалить свой аватар
    /// </summary>
    public override async Task<IActionResult> DeleteAvatarInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await avatarService.DeleteAvatarAsync(user.Id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Загрузить свой аватар
    /// </summary>
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

    /// <summary>
    /// Получить свой аватар
    /// </summary>
    public override async Task<IActionResult> GetAvatarContentInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var userDbModel = await userService.GetUserAsync(user.Id);
        if (userDbModel?.ImageUrl is null)
        {
            return NotFound();
        }

        return await StreamAvatarAsync(userDbModel.ImageUrl, cancellationToken);
    }

    /// <summary>
    /// Получить аватар пользователя из текущего workspace
    /// </summary>
    [WorkspaceRequired]
    [RouteParameterConstraint("userId", "long")]
    public override async Task<IActionResult> GetUserAvatarContentInContext(
        string workspaceId,
        string teamId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        if (user.WorkspaceId is null)
        {
            return NotFound();
        }

        var users = await userService.ListUsersAsync([userId], user.WorkspaceId);
        var userDbModel = users.FirstOrDefault();
        if (userDbModel?.ImageUrl is null)
        {
            return NotFound();
        }

        return await StreamAvatarAsync(userDbModel.ImageUrl, cancellationToken);
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
