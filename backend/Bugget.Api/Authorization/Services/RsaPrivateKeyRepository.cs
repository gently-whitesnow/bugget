using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Api.Authorization.Models;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.Api.Authorization;

public sealed class RsaPrivateKeyRepository : IRsaPrivateKeyStorage
{
    private readonly IReadOnlyList<RsaSecurityKey> _privateKeys;
    public RsaSecurityKey Active => _privateKeys[0];
    public IReadOnlyList<RsaSecurityKey> All => _privateKeys;

    public static RsaPrivateKeyRepository FromPairs(IEnumerable<RsaKeyPair> pairs)
        => new(pairs.Select(p => p.PrivateKey).ToList());

    private RsaPrivateKeyRepository(IReadOnlyList<RsaSecurityKey> keys)
        => _privateKeys = keys;
    public Task<RsaSecurityKey> GetRsaPrivateKeyAsync() => Task.FromResult(Active);
}
