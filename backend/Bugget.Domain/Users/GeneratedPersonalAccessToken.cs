using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Bugget.Domain.Users;

/// <summary>
/// Свежевыпущенный токен. <paramref name="Value"/> существует только в этом ответе:
/// в БД уходит <paramref name="Hash"/>, пользователю — <paramref name="Value"/> один раз.
/// </summary>
public sealed record GeneratedPersonalAccessToken(string Value, string DisplayPrefix, byte[] Hash);
