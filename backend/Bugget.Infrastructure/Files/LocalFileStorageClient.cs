using Bugget.Application.Options;
using Bugget.Application.Ports;
using Microsoft.Extensions.Options;

namespace Bugget.Infrastructure.Files;

/// <summary>
/// Локальное файловое хранилище: запись, чтение, удаление.
/// Каждый вызов к диску идёт через <see cref="FileStorageGate"/>: зависшая сетевая шара роняет только операции с файлами.
/// </summary>
public sealed class LocalFileStorageClient : IFileStorageClient
{
    private readonly string _baseDirectory;
    private readonly FileStorageGate _gate;

    public LocalFileStorageClient(IOptions<FileStorageOptions> fileStorageOptions)
    {
        var options = fileStorageOptions.Value;
        _baseDirectory = options.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/');
        _gate = new FileStorageGate(options.MaxConcurrentOperations, options.OperationTimeout);
    }

    public async Task WriteAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        var path = GetPath(storageKey);

        // Сбрасываем позицию, если можно
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await using var outFs = await _gate.RunAsync(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new GatedFileStream(
                new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 0),
                length: 0,
                _gate);
        }, ct);
        await content.CopyToAsync(outFs, ct);
    }

    public async Task<Stream> ReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetPath(storageKey);
        return await _gate.RunAsync(() =>
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("File not found", storageKey);
            }

            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0);
            return new GatedFileStream(fs, fs.Length, _gate);
        }, ct);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetPath(storageKey);
        return _gate.RunAsync(() =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }, ct);
    }

    private string GetPath(string storageKey) =>
        Path.Combine(_baseDirectory, storageKey.Replace('/', Path.DirectorySeparatorChar));
}
