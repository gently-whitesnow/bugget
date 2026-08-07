using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Bugget.Domain.Users;

/// <summary>
/// Секрет personal access token: генерация, хэш для хранения и распознавание формата.
/// Живёт в домене, потому что нужен обеим сторонам — и выпуску токена, и аутентификации.
/// </summary>
public static class PersonalAccessTokenSecret
{
    /// <summary>
    /// Опознавательный префикс. Нужен не для красоты: по нему секрет-сканеры находят
    /// утёкший токен в чужом репозитории, а схема аутентификации отличает PAT от JWT
    /// в заголовке Authorization, не заглядывая в БД.
    /// </summary>
    public const string Prefix = "bgt_pat_";

    /// <summary>
    /// Сколько символов секрета попадает в открытый префикс записи. Столько же видит
    /// пользователь в списке своих токенов.
    /// </summary>
    private const int DisplaySecretLength = 6;

    /// <summary>
    /// Полная длина открытого префикса значения (<c>bgt_pat_</c> + первые символы
    /// секрета) — то, что видно в списке токенов и чем ключуется троттлинг попыток.
    /// </summary>
    public static readonly int DisplayPrefixLength = Prefix.Length + DisplaySecretLength;

    private const int SecretBytes = 32;

    public static GeneratedPersonalAccessToken Generate()
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        var value = Prefix + secret;

        return new GeneratedPersonalAccessToken(
            value,
            Prefix + secret[..DisplaySecretLength],
            ComputeHash(value));
    }

    public static byte[] ComputeHash(string tokenValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(tokenValue));

    public static bool HasValidFormat(string? tokenValue) =>
        tokenValue is not null
        && tokenValue.StartsWith(Prefix, StringComparison.Ordinal)
        && tokenValue.Length > Prefix.Length + DisplaySecretLength;
}

/// <summary>
/// Свежевыпущенный токен. <paramref name="Value"/> существует только в этом ответе:
/// в БД уходит <paramref name="Hash"/>, пользователю — <paramref name="Value"/> один раз.
/// </summary>
public sealed record GeneratedPersonalAccessToken(string Value, string DisplayPrefix, byte[] Hash);
