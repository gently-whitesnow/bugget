namespace Bugget.Entities.BO.Bugs;

public sealed class BugPatchResult
{
    public required int Id { get; init; }
    public string? Title { get; init; }
    // `patch_bug_internal` возвращает колонки как есть, а `bugs.receive`/`bugs.expect`
    // допускают NULL с миграции 009: баг заводят с одним заполненным полем из пары.
    public string? Receive { get; init; }
    public string? Expect { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required int Status { get; init; }
}
