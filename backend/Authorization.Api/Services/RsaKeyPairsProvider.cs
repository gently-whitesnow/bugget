using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Authorization.Models;
using Microsoft.Extensions.Logging;

namespace Authorization;

/// <summary>
/// Источник RSA-пар для подписи JWT: читает файл, а если его нет — генерирует пару
/// и пытается сохранить по тому же пути.
/// </summary>
/// <remarks>
/// Ключи — секрет и в репозитории не лежат. В self-hosted-контуре путь
/// (<c>KeyStoreOptions.PemFilePath</c>) должен указывать на постоянный том: при генерации
/// нового ключа все ранее выданные токены становятся невалидными, то есть все сессии
/// разлогиниваются.
/// </remarks>
public static class RsaKeyPairsProvider
{
    private const int KeySizeBits = 2048;

    public static async Task<IReadOnlyCollection<RsaKeyPair>> LoadOrCreateAsync(string filePath, ILogger? logger = null)
    {
        if (File.Exists(filePath))
        {
            return await FileRsaKeyPairsLoader.LoadRsaKeyPairsAsync(filePath);
        }

        logger?.LogWarning(
            "Файл ключей {FilePath} не найден — генерируется новая RSA-пара. Ранее выданные токены станут невалидными.",
            filePath);

        var pair = Generate();
        await TryPersistAsync(filePath, pair, logger);

        return [pair];
    }

    private static RsaKeyPair Generate()
    {
        using var rsa = RSA.Create(KeySizeBits);
        return new RsaKeyPair(
            KeyId: Guid.NewGuid().ToString("N"),
            PrivateKeyPem: rsa.ExportPkcs8PrivateKeyPem(),
            PublicKeyPem: rsa.ExportSubjectPublicKeyInfoPem());
    }

    private static async Task TryPersistAsync(string filePath, RsaKeyPair pair, ILogger? logger)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new[] { pair }));
            logger?.LogInformation("Сгенерированная RSA-пара сохранена в {FilePath}", filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Каталог только для чтения — работаем на ключе из памяти. Он живёт до рестарта,
            // после которого сессии разлогинятся.
            logger?.LogError(ex, "Не удалось сохранить RSA-пару в {FilePath}, ключ действует только до перезапуска", filePath);
        }
    }
}
