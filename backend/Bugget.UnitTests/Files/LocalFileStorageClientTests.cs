using Bugget.Application.Options;
using Bugget.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bugget.UnitTests.Files;

public sealed class LocalFileStorageClientTests : IDisposable
{
    private readonly string _baseDirectory = Path.Combine(Path.GetTempPath(), "bugget-storage-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorageClient _client;

    public LocalFileStorageClientTests()
    {
        _client = new LocalFileStorageClient(Options.Create(new FileStorageOptions { BaseDirectory = _baseDirectory }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDirectory))
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
    }

    [Fact(DisplayName = "Записанный файл читается обратно целиком и с длиной")]
    public async Task WriteThenRead()
    {
        var bytes = Enumerable.Range(0, 300_000).Select(i => (byte)i).ToArray();

        await _client.WriteAsync("a/b/file.bin", new MemoryStream(bytes));
        await using var read = await _client.ReadAsync("a/b/file.bin");
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);

        read.Length.Should().Be(bytes.Length);
        copy.ToArray().Should().Equal(bytes);
    }

    [Fact(DisplayName = "Чтение несуществующего файла кидает FileNotFoundException")]
    public async Task ReadMissing()
    {
        var act = () => _client.ReadAsync("missing.bin");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact(DisplayName = "Удаление убирает файл, повторное удаление не падает")]
    public async Task Delete()
    {
        await _client.WriteAsync("file.txt", new MemoryStream([1, 2, 3]));

        await _client.DeleteAsync("file.txt");
        await _client.DeleteAsync("file.txt");

        File.Exists(Path.Combine(_baseDirectory, "file.txt")).Should().BeFalse();
    }

    [Fact(DisplayName = "Повторная запись по тому же ключу не перезаписывает файл")]
    public async Task WriteExistingFails()
    {
        await _client.WriteAsync("file.txt", new MemoryStream([1]));

        var act = () => _client.WriteAsync("file.txt", new MemoryStream([2]));

        await act.Should().ThrowAsync<IOException>();
        File.ReadAllBytes(Path.Combine(_baseDirectory, "file.txt")).Should().Equal(1);
    }

    [Fact(DisplayName = "Оборванная запись не оставляет файл, и ключ можно записать снова")]
    public async Task FailedWriteRemovesPartialFile()
    {
        var act = () => _client.WriteAsync("file.bin", new FailingStream(bytesBeforeFailure: 100_000));

        await act.Should().ThrowAsync<IOException>().WithMessage("source broke");
        File.Exists(Path.Combine(_baseDirectory, "file.bin")).Should().BeFalse();

        await _client.WriteAsync("file.bin", new MemoryStream([1, 2, 3]));
        File.ReadAllBytes(Path.Combine(_baseDirectory, "file.bin")).Should().Equal(1, 2, 3);
    }

    private sealed class FailingStream(int bytesBeforeFailure) : Stream
    {
        private int _left = bytesBeforeFailure;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_left == 0)
            {
                throw new IOException("source broke");
            }

            var read = Math.Min(count, _left);
            _left -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
