using System.Text.Json;
using Bugget.BO.Ports;
using Bugget.DA.Transactions;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.DTO;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.DTO.BugStep;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class BugStepsDbClient : PostgresClient, IBugStepsDbClient
{
    public async Task<BugStepSummary> CreateBugStepAsync(string userId, int bugId, BugStepDto createDto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<BugStepSummary>(
            "SELECT * FROM public.create_bug_step_internal(@user_id, @bug_id, @text);",
            new
            {
                user_id = userId,
                bug_id = bugId,
                text = createDto.Text,
            }
        );
    }

    public Task<BugStepSummary> CreateBugStepAsync(
        ITransactionScope scope,
        string userId,
        int bugId,
        BugStepDto createDto)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<BugStepSummary>(new CommandDefinition(
            "SELECT * FROM public.create_bug_step_internal(@user_id, @bug_id, @text);",
            new
            {
                user_id = userId,
                bug_id = bugId,
                text = createDto.Text,
            },
            transaction: tx));
    }

    public async Task<BugStepSummary?> DeleteBugStepInternalAsync(int reportId, int bugId, int stepId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<BugStepSummary>(
            "SELECT public.delete_bug_step_internal(@report_id, @bug_id, @step_id);",
            new
            {
                report_id = reportId,
                bug_id = bugId,
                step_id = stepId
            }
        );
    }

    public async Task<BugStepSummary> PatchBugStepInternalAsync(int reportId, int bugId, int stepId, BugStepDto patchDto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<BugStepSummary>(
            "SELECT * FROM public.patch_bug_step_internal(@report_id, @bug_id, @step_id, @text);",
            new
            {
                report_id = reportId,
                bug_id = bugId,
                step_id = stepId,
                text = patchDto.Text
            }
        );
    }

    public async Task<BugStepSummary[]> UpdateBugStepsOrderInternalAsync(int reportId, int bugId, BugStepsOrderDto orderDto)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var results = await connection.QueryAsync<BugStepSummary>(
            "SELECT * FROM public.update_bug_steps_order_internal(@report_id, @bug_id, @step_ids);",
            new
            {
                report_id = reportId,
                bug_id = bugId,
                step_ids = orderDto.StepIds
            }
        );

        return results.ToArray();
    }

    public async Task<BugStepSummary[]> ListBugStepsInternalAsync(int reportId, int bugId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return (await connection.QueryAsync<BugStepSummary>(
            "SELECT * FROM public.list_bug_steps_internal(@report_id, @bug_id);",
            new { report_id = reportId, bug_id = bugId }
        )).ToArray();
    }
}
