using System.Net;
using Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.BO.Interfaces;
using Users.DA.Interfaces;

namespace Users.Api.Controllers.Users;

[Auth]
[Route("v1/users")]
[Route("v1/workspaces/{workspaceId}/teams/{teamId}/users")]
public sealed class AvatarController(
    IUsersService userService,
    IAvatarDownloadService avatarService,
    IFileStorageClient fileStorageClient) : ApiController
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
    [HttpDelete("avatar")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> DeleteAvatarAsync(CancellationToken ct)
    {
        var user = User.GetIdentity();
        await avatarService.DeleteAvatarAsync(user.Id, ct);
        return NoContent();
    }

    /// <summary>
    /// Загрузить свой аватар
    /// </summary>
    [HttpPost("avatar")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> UploadAvatarAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > MaxAvatarSize)
        {
            return BadRequest("Размер файла не должен превышать 200 КБ");
        }

        if (!AllowedAvatarContentTypes.Contains(file.ContentType))
        {
            return BadRequest("Недопустимый формат файла. Разрешены: JPEG, PNG, GIF, WebP");
        }

        var user = User.GetIdentity();
        await using var stream = file.OpenReadStream();
        await avatarService.UploadAvatarAsync(user.Id, stream, file.ContentType, ct);
        return Ok();
    }

    /// <summary>
    /// Получить свой аватар
    /// </summary>
    [HttpGet("avatar/content")]
    [ProducesResponseType(typeof(FileStreamResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetAvatarContentAsync(CancellationToken ct)
    {
        var user = User.GetIdentity();
        var userDbModel = await userService.GetUserAsync(user.Id);
        if (userDbModel?.ImageUrl is null)
        {
            return NotFound();
        }

        return await StreamAvatarAsync(userDbModel.ImageUrl, ct);
    }

    /// <summary>
    /// Получить аватар пользователя из текущего workspace
    /// </summary>
    [WorkspaceRequired]
    [HttpGet("{userId:long}/avatar/content")]
    [ProducesResponseType(typeof(FileStreamResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetUserAvatarContentAsync([FromRoute] long userId, CancellationToken ct)
    {
        var user = User.GetIdentity();
        if (user.WorkspaceId is null)
        {
            return NotFound();
        }

        var users = await userService.ListUsersAsync(new[] { userId }, user.WorkspaceId);
        var userDbModel = users.FirstOrDefault();
        if (userDbModel?.ImageUrl is null)
        {
            return NotFound();
        }

        return await StreamAvatarAsync(userDbModel.ImageUrl, ct);
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
