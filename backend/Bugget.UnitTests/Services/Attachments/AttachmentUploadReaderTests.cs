using Bugget.Api.Controllers.Attachments;
using FluentAssertions;

namespace Bugget.UnitTests.Services.Attachments;

/// <summary>
/// В development MIME берётся из заголовка клиента, а браузер дописывает к нему
/// параметры. Белый список сравнивает тип целиком, и cURL-вложение
/// <c>text/plain;charset=utf-8</c> отклонялось как неподдерживаемое.
/// </summary>
public sealed class AttachmentUploadReaderTests
{
    [Theory(DisplayName = "Параметры MIME отрезаются, тип остаётся")]
    [InlineData("text/plain;charset=utf-8", "text/plain")]
    [InlineData("text/plain; charset=UTF-8", "text/plain")]
    [InlineData("application/json", "application/json")]
    [InlineData(" image/png ", "image/png")]
    [InlineData("", "")]
    public void StripsMimeParameters(string contentType, string expected)
    {
        AttachmentUploadReader.StripMimeParameters(contentType).Should().Be(expected);
    }
}
