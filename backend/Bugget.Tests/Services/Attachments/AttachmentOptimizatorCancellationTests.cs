using Bugget.BO.Services.Attachments;
using FluentAssertions;

namespace Bugget.Tests.Services.Attachments;

public sealed class AttachmentOptimizatorCancellationTests
{
    [Fact(DisplayName = "Runtime cancellation доходит до оптимизатора вложения")]
    public void Runtime_cancellation_reaches_attachment_optimizer()
    {
        var parameters = typeof(AttachmentOptimizator)
            .GetMethod(nameof(AttachmentOptimizator.OptimizeAttachmentAsync))!
            .GetParameters();

        parameters.Should().Contain(
            parameter => parameter.ParameterType == typeof(CancellationToken),
            "TaskQueue передаёт stoppingToken, который обязан дойти до ожидающего или активного ffmpeg");
    }
}
