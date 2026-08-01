using Bugget.Application.ExternalSearch.Models;
using Bugget.Domain.Authentication;

namespace Bugget.Application.Interfaces;

public interface IExternalApplyRepository
{
    Task ApplySearchResultAsync(UserIdentity user, string workspaceId, string teamId, ExternalSearchApply searchApply);
}
