using Bugget.Application.Errors;
using Bugget.Application.ExternalSearch.Models;
using Bugget.Application.ExternalSearch.Ports;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Services.External;

public sealed class ExternalSearchService(
    IEnumerable<IExternalSearchRepository> externalSearchRepositories,
    IEnumerable<IExternalApplyRepository> externalApplyRepositories,
    ILogger<ExternalSearchService> logger,
    ITaskQueue taskQueue,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions)
{
    private static readonly ExternalSearchResult EmptyExternalSearchResult = new() { Total = 0, Items = [] };

    public async Task<ExternalSearchResult> SearchAsync(string workspaceId, string teamId, string? query, uint skip, uint take)
    {
        if (!externalSearchRepositories.Any())
        {
            return EmptyExternalSearchResult;
        }

        var searchTasks = externalSearchRepositories.Select(repository => SafeSearchAsync(repository, workspaceId, teamId, query, skip, take));
        var searchResults = await Task.WhenAll(searchTasks);

        return new ExternalSearchResult
        {
            Total = searchResults.Sum(result => result?.Total ?? 0),
            Items = searchResults.SelectMany(result => result?.Items ?? []).ToArray()
        };
    }

    public async Task<Error?> ApplySearchResultAsync(
        UserIdentity user,
         string workspaceId,
          string teamId,
           string id,
            string source,
             string aliasId)
    {
        if (!externalApplyRepositories.Any())
        {
            return null;
        }

        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var searchApply = new ExternalSearchApply
        {
            Id = id,
            Source = source,
            reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId)
        };
        await taskQueue.EnqueueAsync(async () =>
        {
            foreach (var repository in externalApplyRepositories)
            {
                await repository.ApplySearchResultAsync(user, workspaceId, teamId, searchApply);
            }
        });
        return null;
    }

    private async Task<ExternalSearchResult?> SafeSearchAsync(IExternalSearchRepository externalSearchRepository, string workspaceId, string teamId, string? query, uint skip, uint take)
    {
        try
        {
            return await externalSearchRepository.SearchAsync(workspaceId, teamId, query, skip, take);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "При выполнении поиска по внешнему источнику {@Repository} произошла ошибка. Запрос: {@Query}, Скип: {@Skip}, Тайк: {@Take}", externalSearchRepository.GetType().Name, query, skip, take);
            return null;
        }
    }
}
