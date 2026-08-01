using Bugget.Application.ExternalSearch.Models;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services.External;

public interface IExternalSearchService
{
    Task<ExternalSearchResult> SearchAsync(string workspaceId, string teamId, string? query, uint skip, uint take);
    Task<Error?> ApplySearchResultAsync(UserIdentity user, string workspaceId, string teamId, string id, string source, string aliasId);
}
