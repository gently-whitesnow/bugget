namespace Bugget.Api.Users.Controllers.Users;

public sealed record ExternalLinkView(string Provider, string ExternalId, string? Email, DateTimeOffset LinkedAt);
