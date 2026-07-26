using Microsoft.Extensions.Logging;
using Users.BO.Interfaces;
using Users.DA.Interfaces;

namespace Users.BO.Avatars;

public class AvatarDownloadService(
    IHttpClientFactory httpClientFactory,
    IFileStorageClient fileStorageClient,
    IUsersRepository usersRepository,
    ILogger<AvatarDownloadService> logger) : IAvatarDownloadService
{
    private static readonly Dictionary<string, string> ContentTypeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp"
    };

    public async Task DownloadAndSaveAvatarAsync(long userId, string externalImageUrl, CancellationToken ct = default)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient("AvatarDownload");
            using var response = await httpClient.GetAsync(externalImageUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Не удалось скачать аватар для пользователя {UserId} с URL {Url}: {StatusCode}",
                    userId, externalImageUrl, response.StatusCode);
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var extension = GetExtensionFromContentType(contentType);

            var storageKey = GetStorageKey(userId, extension);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await fileStorageClient.WriteAsync(storageKey, stream, ct);

            await usersRepository.UpdateUserImageUrlAsync(userId, storageKey);

            logger.LogInformation("Аватар пользователя {UserId} успешно сохранён: {StorageKey}", userId, storageKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке аватара для пользователя {UserId} с URL {Url}",
                userId, externalImageUrl);
        }
    }

    public async Task DeleteAvatarAsync(long userId, CancellationToken ct = default)
    {
        var user = await usersRepository.GetUserAsync(userId);
        if (user?.ImageUrl is null)
        {
            return;
        }

        try
        {
            var deleteFileTask = fileStorageClient.DeleteAsync(user.ImageUrl, ct);
            var updateUserTask = usersRepository.UpdateUserImageUrlAsync(userId, null);
            await Task.WhenAll(deleteFileTask, updateUserTask);
            logger.LogInformation("Аватар пользователя {UserId} успешно удалён", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении аватара пользователя {UserId}", userId);
            throw;
        }
    }

    public async Task UploadAvatarAsync(long userId, Stream content, string contentType, CancellationToken ct = default)
    {
        var extension = GetExtensionFromContentType(contentType);
        var storageKey = GetStorageKey(userId, extension);

        try
        {
            var currentUserTask = usersRepository.GetUserAsync(userId);
            var writeFileTask = fileStorageClient.WriteAsync(storageKey, content, ct);
            await Task.WhenAll(currentUserTask, writeFileTask);
            var oldImageUrl = currentUserTask.Result?.ImageUrl;

            await usersRepository.UpdateUserImageUrlAsync(userId, storageKey);
            if (oldImageUrl is not null)
            {
                try
                {
                    await fileStorageClient.DeleteAsync(oldImageUrl, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Не удалось удалить старый аватар {OldImageUrl}", oldImageUrl);
                }
            }

            logger.LogInformation("Аватар пользователя {UserId} успешно загружен: {StorageKey}", userId, storageKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке аватара для пользователя {UserId}", userId);
            throw;
        }
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return ContentTypeToExtension.TryGetValue(contentType, out var ext) ? ext : ".jpg";
    }

    private static string GetStorageKey(long userId, string extension)
    {
        return $"avatars/{userId}/{Guid.NewGuid()}{extension}";
    }
}
