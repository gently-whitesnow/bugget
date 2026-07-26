namespace Users.BO.Interfaces;

public interface IAvatarDownloadService
{
    Task DownloadAndSaveAvatarAsync(long userId, string externalImageUrl, CancellationToken ct = default);
    Task DeleteAvatarAsync(long userId, CancellationToken ct = default);
    Task UploadAvatarAsync(long userId, Stream content, string contentType, CancellationToken ct = default);
}
