using Bugget.Domain.Users;

namespace Bugget.Application.Users.Results.PersonalAccessTokens;

/// <summary>
/// Результат выпуска: сохранённая запись и открытое значение секрета. Живёт только
/// в ответе на выпуск — нигде не сохраняется и не логируется.
/// </summary>
public sealed record IssuedPersonalAccessToken(PersonalAccessToken Token, string SecretValue);
