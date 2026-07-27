using Authentication;
using Flow.Routing;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Generated;
using Users.BO.Interfaces;
using Users.DA.Interfaces;
using FileParameter = Users.Api.Generated.FileParameter;

namespace Users.Api.Controllers.Users;

/// <summary>
/// Аватары пользователей. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="AvatarControllerBase"/>.
/// </summary>
/// <remarks>
/// Как и профиль, каждая ручка объявлена дважды — с контекстом рабочего
/// пространства и команды и без него; контекстные операции делегируют
/// бесконтекстным.
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
    public override async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await avatarService.DeleteAvatarAsync(user.Id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Загрузить свой аватар
    /// </summary>
    public override async Task<IActionResult> UploadAvatar(
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default)
    {
        var content = file.Data;
        if (content.Length > MaxAvatarSize)
        {
            return BadRequest("Размер файла не должен превышать 200 КБ");
        }

        if (!AllowedAvatarContentTypes.Contains(file.ContentType))
        {
            return BadRequest("Недопустимый формат файла. Разрешены: JPEG, PNG, GIF, WebP");
        }

        var user = User.GetIdentity();
        await using var stream = content;
        await avatarService.UploadAvatarAsync(user.Id, stream, file.ContentType, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Получить свой аватар
    /// </summary>
    public override async Task<IActionResult> GetAvatarContent(CancellationToken cancellationToken = default)
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
    public override async Task<IActionResult> GetUserAvatarContent(
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

    // --- те же операции по адресу с контекстом рабочего пространства и команды ---

    public override Task<IActionResult> DeleteAvatarInContext(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default) => DeleteAvatar(cancellationToken);

    public override Task<IActionResult> UploadAvatarInContext(
        int workspaceId,
        int teamId,
        [FromForm] FileParameter file,
        CancellationToken cancellationToken = default) => UploadAvatar(file, cancellationToken);

    public override Task<IActionResult> GetAvatarContentInContext(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default) => GetAvatarContent(cancellationToken);

    [WorkspaceRequired]
    [RouteParameterConstraint("userId", "long")]
    public override Task<IActionResult> GetUserAvatarContentInContext(
        int workspaceId,
        int teamId,
        long userId,
        CancellationToken cancellationToken = default) => GetUserAvatarContent(userId, cancellationToken);

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
