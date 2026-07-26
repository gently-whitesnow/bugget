namespace Authentication;

public class AuthHeadersOptions
{
    public string? UserIdHeaderName { get; set; }
    public string? TeamIdHeaderName { get; set; }
    public string? WorkspaceIdHeaderName { get; set; }
    public string? WorkspaceRoleHeaderName { get; set; }
}
