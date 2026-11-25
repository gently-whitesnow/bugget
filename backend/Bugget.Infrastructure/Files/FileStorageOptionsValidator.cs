using Bugget.Application.Options;
using Microsoft.Extensions.Options;

namespace Bugget.Infrastructure.Files;

/// <summary>
/// Проверка лимитов хранилища на старте (<c>ValidateOnStart</c>): иначе кривое значение падает
/// при первом обращении к синглтону, и каждая операция с файлами отвечает 500.
/// </summary>
public sealed class FileStorageOptionsValidator : IValidateOptions<FileStorageOptions>
{
    // Потолок SemaphoreSlim.WaitAsync(TimeSpan).
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    public ValidateOptionsResult Validate(string? name, FileStorageOptions options)
    {
        var failures = new List<string>();

        if (options.MaxConcurrentOperations < 1)
        {
            failures.Add($"{nameof(options.MaxConcurrentOperations)} должен быть не меньше 1, сейчас {options.MaxConcurrentOperations}.");
        }

        if (options.OperationTimeout <= TimeSpan.Zero || options.OperationTimeout > MaxTimeout)
        {
            failures.Add($"{nameof(options.OperationTimeout)} должен быть больше нуля и не больше {MaxTimeout}, сейчас {options.OperationTimeout}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
