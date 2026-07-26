using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Tests.TokensService;

public static class RsaKeysMock
{
    public static (RsaSecurityKey priv, JsonWebKey pub) Create(string kid)
    {
        var rsa = RSA.Create(2048);
        var priv = new RsaSecurityKey(rsa) { KeyId = kid };
        var pub = JsonWebKeyConverter.ConvertFromRSASecurityKey(priv);
        return (priv, pub);
    }
}
