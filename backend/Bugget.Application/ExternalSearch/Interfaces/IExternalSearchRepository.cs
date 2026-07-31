using Bugget.Application.ExternalSearch.Models;

namespace Bugget.Application.Interfaces;

public interface IExternalSearchRepository
{
    Task<ExternalSearchResult> SearchAsync(string workspaceId, string teamId, string? query, uint skip, uint take);
}
