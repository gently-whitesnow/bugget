using Bugget.BO.Errors;
using Bugget.BO.Services.Idempotency;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.DTO.BugStep;
using Bugget.Entities.DTO.Internal;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// POST /v2/_internal/bugs/{bugId}/steps — создание одного шага воспроизведения от имени
/// внешнего автора (TgBetaTester) с idempotency по `Idempotency-Key`. Caller (бот) шлёт
/// шаги последовательно `1..N`, ключ — `{bug_idem_key}-step-{n}`. Успешный результат
/// кэшируется на 24h; повторный вызов с тем же ключом отдаёт закешированный stepId.
/// Domain event пока не эмитим — потребителя `bug_step.created` в системе нет.
/// </summary>
public sealed class InternalBugStepsService(
    IBugsDbClient bugsDbClient,
    IBugStepsDbClient bugStepsDbClient,
    IdempotencyCacheService idempotencyCacheService,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public async Task<MonadeStruct<InternalCreateBugStepResponseDto>> CreateAsync(
        string idempotencyKey,
        int bugId,
        InternalCreateBugStepRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BoErrors.IdempotencyKeyRequired;
        }

        if (string.IsNullOrWhiteSpace(request.CreatorUserId))
        {
            return BoErrors.IdempotencyKeyRequired;
        }

        var locator = await bugsDbClient.GetBugLocatorAsync(bugId);
        if (locator is null)
        {
            return BoErrors.BugNotFoundError;
        }

        return await unitOfWork.ExecuteAsync((scope, c) =>
            idempotencyCacheService.GetOrComputeInScopeAsync<InternalCreateBugStepResponseDto>(
                scope,
                idempotencyKey,
                IdempotencyTtl,
                async innerCt =>
                {
                    var step = await bugStepsDbClient.CreateBugStepAsync(
                        scope,
                        userId: request.CreatorUserId,
                        bugId: bugId,
                        createDto: new BugStepDto { Text = request.Text });

                    return (MonadeStruct<InternalCreateBugStepResponseDto>)new InternalCreateBugStepResponseDto
                    {
                        StepId = step.Id,
                        StepNumber = step.StepNumber,
                    };
                },
                c),
            ct);
    }
}
