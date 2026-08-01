using Bugget.Application.Commands.Link;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.ReportLinks;

/// <summary>
/// Узкая внутренняя граница создания ссылки отчёта: отчёт уже разрешён в
/// <see cref="ReportIdContext"/>, alias-резолв и HTTP-сценарии сюда не входят.
///
/// Контракт прикладного слоя, а не порт внешней зависимости: его реализует сам
/// <see cref="ReportLinksService"/>, а вызывает адаптер (сегодня — применение результата
/// поиска Kaiten), которому из широкого <see cref="IReportLinksService"/> нужна была
/// ровно одна операция. Контракт provider-neutral: ни Kaiten-типов, ни URL, ни досок.
/// </summary>
public interface IReportLinkCreator
{
    Task<(ReportLink? Value, Error? Error)> CreateReportLinkInternalAsync(
        UserIdentity user,
        ReportIdContext reportIdContext,
        ReportLinkDto dto);
}
