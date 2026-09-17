namespace Bugget.Api.Mcp;

/// <summary>
/// Пагинация текстового вложения: сколько символов всего, что отдано и остался
/// ли хвост. Поля явные — «обрезали молча» для модели неотличимо от «файл
/// закончился».
/// </summary>
internal sealed record McpTextPage(
    int TotalChars,
    int Offset,
    int ReturnedChars,
    bool Truncated);
