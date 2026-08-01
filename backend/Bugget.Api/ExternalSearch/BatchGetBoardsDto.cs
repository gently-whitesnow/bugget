using System.ComponentModel.DataAnnotations;

namespace Bugget.Api.ExternalSearch;

public sealed class BatchGetBoardsDto
{
    [Length(1, 100)]
    public required int[] Ids { get; set; }
}
