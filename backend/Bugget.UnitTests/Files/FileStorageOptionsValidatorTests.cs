using Bugget.Application.Options;
using Bugget.Infrastructure.Files;
using FluentAssertions;
using Xunit;

namespace Bugget.UnitTests.Files;

public sealed class FileStorageOptionsValidatorTests
{
    private readonly FileStorageOptionsValidator _validator = new();

    [Fact(DisplayName = "Значения по умолчанию проходят проверку")]
    public void DefaultsAreValid()
    {
        var result = _validator.Validate(null, new FileStorageOptions { BaseDirectory = "/data" });

        result.Succeeded.Should().BeTrue();
    }

    [Theory(DisplayName = "Нулевой, бесконечный и слишком длинный таймаут отклоняются на старте")]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    [InlineData("30.00:00:00")]
    public void RejectsBadTimeout(string timeout)
    {
        var options = new FileStorageOptions { BaseDirectory = "/data", OperationTimeout = TimeSpan.Parse(timeout, System.Globalization.CultureInfo.InvariantCulture) };

        var result = _validator.Validate("users", options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(FileStorageOptions.OperationTimeout));
    }

    [Fact(DisplayName = "Ноль одновременных операций отклоняется на старте")]
    public void RejectsZeroConcurrency()
    {
        var options = new FileStorageOptions { BaseDirectory = "/data", MaxConcurrentOperations = 0 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(FileStorageOptions.MaxConcurrentOperations));
    }
}
