using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Bugget.Domain.Users;

/// <summary>Секрет PAT: генерация, хэш и формат. В домене, потому что нужен и выпуску токена, и аутентификации.</summary>
public static class PersonalAccessTokenSecret
{
    /// <summary>
    /// Опознавательный префикс: по нему секрет-сканеры находят утёкший токен, а схема аутентификации
    /// отличает PAT от JWT в заголовке Authorization, не заглядывая в БД.
    /// </summary>
    public const string Prefix = "bgt_pat_";

    /// <summary>Сколько символов секрета попадает в открытый префикс записи (видно в списке токенов).</summary>
    private const int DisplaySecretLength = 6;

    /// <summary>Полная длина открытого префикса значения; им же ключуется троттлинг попыток.</summary>
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
