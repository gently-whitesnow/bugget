using System;
using System.Threading.Tasks;

namespace Bugget.Application.Authorization.Ports;

public interface IRefreshRevocationStore
{
    Task<bool> IsRevokedAsync(string jti);
    Task RevokeAsync(string jti, DateTimeOffset expires);
}
