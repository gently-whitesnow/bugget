using System.Text.Json.Serialization;

namespace MattermostOAuth.Models;

public sealed class MattermostUserResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
