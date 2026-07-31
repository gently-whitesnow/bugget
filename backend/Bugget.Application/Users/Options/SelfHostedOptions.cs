namespace Bugget.Application.Users.Options;

public class SelfHostedOptions
{
    public string DefaultWorkspaceName { get; set; } = "Рабочая область";
    public string DefaultTeamName { get; set; } = "Гость";
    public bool Enabled { get; set; } = true;
}
