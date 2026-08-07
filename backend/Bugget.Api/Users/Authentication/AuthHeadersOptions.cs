namespace Bugget.Api.Users.Authentication;

public class AuthHeadersOptions
{
    public string? UserIdHeaderName { get; set; }
    public string? TeamIdHeaderName { get; set; }
    public string? WorkspaceIdHeaderName { get; set; }
    public string? WorkspaceRoleHeaderName { get; set; }

    /// <summary>
    /// Способ входа (<c>pat</c>/<c>jwt</c>). Опционален: без заголовка поведение как у JWT-сессии.
    /// </summary>
    public string? AuthMethodHeaderName { get; set; }
}
