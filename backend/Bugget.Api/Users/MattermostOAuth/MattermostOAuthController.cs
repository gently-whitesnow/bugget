using Bugget.Api.Users.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Users.MattermostOAuth;

[Auth]
[ApiController, Route("v1/users/mattermost")]
public sealed class MattermostOAuthController(
    MattermostOAuthClient client,
    IMattermostUserUpdater userUpdater,
    IOptions<MattermostOAuthOptions> options,
    ILogger<MattermostOAuthController> logger) : ControllerBase
{
    private readonly MattermostOAuthOptions _options = options.Value;

    [HttpGet("connect")]
    public IActionResult Connect()
    {
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/users/v1/users/mattermost/callback";

        var authorizeUrl = $"{_options.BaseUrl.TrimEnd('/')}/oauth/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}";

        return Redirect(authorizeUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        var identity = User.GetIdentity();
        var userId = identity.Id;

        if (userId <= 0)
        {
            return Unauthorized();
        }

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/users/v1/users/mattermost/callback";

        var accessToken = await client.ExchangeCodeAsync(code, callbackUrl);
        var mmUser = await client.GetCurrentUserAsync(accessToken);

        logger.LogInformation(
            "Mattermost OAuth callback: userId={UserId}, mmUserId={MmUserId}, mmUsername={MmUsername}",
            userId, mmUser.Id, mmUser.Username);

        await userUpdater.UpdateMattermostUserIdAsync(userId, mmUser.Id);

        var domain = Environment.GetEnvironmentVariable("APP_DOMAIN") ?? "";
        return Redirect(domain + _options.SuccessRedirectPath);
    }
}
