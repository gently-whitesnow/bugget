using System;
using System.Threading.Tasks;

public interface IRefreshRevocationStore
{
    Task<bool> IsRevokedAsync(string jti);
    Task RevokeAsync(string jti, DateTimeOffset expires);
}
