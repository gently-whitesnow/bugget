using Authorization.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Authorization.Tests.TokensService;

public sealed class PrivateKeyStorageMock : IRsaPrivateKeyStorage
{
    private readonly RsaSecurityKey _key;
    public PrivateKeyStorageMock(RsaSecurityKey key) => _key = key;
    public Task<RsaSecurityKey> GetRsaPrivateKeyAsync() => Task.FromResult(_key);
}
