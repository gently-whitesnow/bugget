using Bugget.BO.ExternalSearch.Models;

namespace Bugget.BO.Interfaces;

public interface IExternalSearchRepository
{
    Task<ExternalSearchResult> SearchAsync(string workspaceId, string teamId, string? query, uint skip, uint take);
}
