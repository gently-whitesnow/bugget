using Microsoft.AspNetCore.Http;

namespace OidcAuth;

public static class OidcTokenExtractor
{
    public static string? Extract(HttpRequest request, OidcAuthOptions options)
    {
        if (!string.IsNullOrEmpty(options.TokenHeaderName))
        {
            var headerValue = request.Headers[options.TokenHeaderName].ToString();
            if (!string.IsNullOrEmpty(headerValue))
            {
                if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return headerValue.Substring("Bearer ".Length).Trim();
                }

                return headerValue;
            }
        }

        if (!string.IsNullOrEmpty(options.CookieName) &&
            request.Cookies.TryGetValue(options.CookieName, out var token) &&
            !string.IsNullOrEmpty(token))
        {
            return token;
        }

        var fallbackNames = new[] { "_oauth2_proxy", "oauth2_proxy_session", "id_token" };
        foreach (var name in fallbackNames)
        {
            if (request.Cookies.TryGetValue(name, out var fallback) && !string.IsNullOrEmpty(fallback))
            {
                return fallback;
            }
        }

        return null;
    }
}
