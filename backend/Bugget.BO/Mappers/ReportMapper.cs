using Bugget.BO.Services.Reports;
using Bugget.Entities.BO;
using Bugget.Entities.BO.Search;
using Bugget.Entities.DbModels.Report;
using Bugget.Entities.DTO.Report;
using Bugget.Entities.Options;
using Bugget.Entities.SocketViews;
using Bugget.Entities.Views.Reports;

namespace Bugget.BO.Mappers;

public static class ReportMapper
{
    public static SearchReports ToSearchReports(
        string? query,
        int[]? reportStatuses,
        string? userId,
        string? teamId,
        string? organizationId,
        string? sort,
        uint skip,
        uint take,
        short[]? creatorTypes = null)
    {
        List<string> resultUserIds = [];
        if (!string.IsNullOrEmpty(userId))
        {
            resultUserIds.Add(userId);
        }

        return new SearchReports
        {
            Query = string.IsNullOrEmpty(query) ? null : query,
            ReportStatuses = reportStatuses?.Length > 0 ? reportStatuses : null,
            UserIds = resultUserIds.Count > 0 ? resultUserIds.ToArray() : null,
            TeamId = teamId,
            Skip = skip,
            Take = take,
            Sort = SortOption.Parse(sort),
            OrganizationId = organizationId,
            CreatorTypes = creatorTypes?.Length > 0 ? creatorTypes : null
        };
    }

    public static PatchReportSocketView ToSocketView(this ReportPatchDto patchDto, ReportPatchResultDbModel result)
    {
        return new PatchReportSocketView
        {
            Title = patchDto.Title,
            Status = patchDto.Status,
            ResponsibleUserId = patchDto.ResponsibleUserId,
            PastResponsibleUserId = patchDto.ResponsibleUserId == null ? null : result.PastResponsibleUserId,
            UpdatedAt = result.UpdatedAt
        };
    }

    public static ReportSummaryViewModel ToViewModel(this ReportSummaryDbModel summaryDbModel, ReportAliasOptions aliasOptions)
    {
        return new ReportSummaryViewModel
        {
            Id = ReportIdResolveHelper.ToAliasId(summaryDbModel.Id, summaryDbModel.PublicId, summaryDbModel.TeamReportId, aliasOptions),
            Title = summaryDbModel.Title,
            Status = summaryDbModel.Status,
            ResponsibleUserId = summaryDbModel.ResponsibleUserId,
            PastResponsibleUserId = summaryDbModel.PastResponsibleUserId,
            CreatorUserId = summaryDbModel.CreatorUserId,
            CreatorTeamId = summaryDbModel.CreatorTeamId,
            CreatedAt = summaryDbModel.CreatedAt,
            UpdatedAt = summaryDbModel.UpdatedAt,
            CreatorType = summaryDbModel.CreatorType
        };
    }

    public static ReportViewModel ToViewModel(this ReportDbModel dbModel, ReportAliasOptions aliasOptions)
    {
        return new ReportViewModel
        {
            Id = ReportIdResolveHelper.ToAliasId(dbModel.Id, dbModel.PublicId, dbModel.TeamReportId, aliasOptions),
            Title = dbModel.Title,
            Status = dbModel.Status,
            ResponsibleUserId = dbModel.ResponsibleUserId,
            PastResponsibleUserId = dbModel.PastResponsibleUserId,
            CreatorUserId = dbModel.CreatorUserId,
            CreatorTeamId = dbModel.CreatorTeamId,
            CreatedAt = dbModel.CreatedAt,
            UpdatedAt = dbModel.UpdatedAt,
            CreatorType = dbModel.CreatorType,
            IsExcludedFromAnalytics = dbModel.IsExcludedFromAnalytics,
            ParticipantsUserIds = dbModel.ParticipantsUserIds,
            Links = dbModel.Links,
            Bugs = dbModel.Bugs
        };
    }

    public static ReportViewModel[] ToViewModel(this ReportDbModel[] dbModels, ReportAliasOptions aliasOptions)
    {
        return dbModels.Select(dbModel => dbModel.ToViewModel(aliasOptions)).ToArray();
    }

    public static ReportPatchResultViewModel ToPatchResultViewModel(this ReportPatchResultDbModel result, ReportAliasOptions aliasOptions)
    {
        return new ReportPatchResultViewModel
        {
            Id = ReportIdResolveHelper.ToAliasId(result.Id, result.PublicId, result.TeamReportId, aliasOptions),
            Title = result.Title,
            Status = result.Status,
            ResponsibleUserId = result.ResponsibleUserId,
            PastResponsibleUserId = result.PastResponsibleUserId,
            UpdatedAt = result.UpdatedAt
        };
    }
}
