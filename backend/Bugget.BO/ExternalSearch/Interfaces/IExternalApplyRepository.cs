using Bugget.BO.ExternalSearch.Models;
using Bugget.Entities.Authentication;

namespace Bugget.BO.Interfaces;

public interface IExternalApplyRepository
{
    Task ApplySearchResultAsync(UserIdentity user, string workspaceId, string teamId, ExternalSearchApply searchApply);
}
