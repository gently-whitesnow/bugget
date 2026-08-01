using Microsoft.Extensions.Configuration;

namespace Bugget.Api.Authorization.Services;

public interface IRedirectService
{
    string GetRedirectUrl();
}

public class RedirectService : IRedirectService
{
    private readonly IConfiguration _configuration;

    public RedirectService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetRedirectUrl()
    {
        return _configuration["DomainOptions:BaseUrl"] ?? "http://localhost";
    }
}
