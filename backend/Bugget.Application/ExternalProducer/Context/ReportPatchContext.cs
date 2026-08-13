using Bugget.Application.Commands.Report;
using Bugget.Domain.Common;
using Bugget.Domain.Reports;

namespace Bugget.Application.ExternalProducer.Context;

/// <summary>
/// <paramref name="ActorCreatorType"/> — кто патчил: человек или агент по PAT.
/// Уведомлению этого мало не бывает: <paramref name="UserId"/> у агента указывает
/// на владельца токена, и подписывать им действие агента нельзя (kaiten 237718).
/// </summary>
public record ReportPatchContext(
    string UserId,
    ReportPatchDto PatchDto,
    ReportPatchResult Result,
    CreatorType ActorCreatorType);
