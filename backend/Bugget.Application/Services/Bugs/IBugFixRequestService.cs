using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services.Bugs;

public interface IBugFixRequestService
{
    /// <summary><c>null</c> — принято (в том числе идемпотентный повтор в кулдауне).</summary>
    Task<Error?> RequestFixAsync(UserIdentity user, string aliasId, int bugId);
}
