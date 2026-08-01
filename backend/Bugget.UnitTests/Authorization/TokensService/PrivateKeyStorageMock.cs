using Bugget.Api.Authorization.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.UnitTests.Authorization.TokensService;

public sealed class PrivateKeyStorageMock : IRsaPrivateKeyStorage
{
    private readonly RsaSecurityKey _key;
    public PrivateKeyStorageMock(RsaSecurityKey key) => _key = key;
    public Task<RsaSecurityKey> GetRsaPrivateKeyAsync() => Task.FromResult(_key);
}
