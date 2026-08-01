using System.Text.Json.Serialization;

namespace Bugget.Api.Users.MattermostOAuth.Models;

public sealed class MattermostUserResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
