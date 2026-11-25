namespace Bugget.Infrastructure.Files;

/// <summary>
/// Поток файла из хранилища, у которого каждое чтение, запись и закрытие идут через <see cref="FileStorageGate"/>.
/// </summary>
internal sealed class GatedFileStream(FileStream inner, long length, FileStorageGate gate) : Stream
{
    private long _length = length;
    private bool _innerDisposed;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => _length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    // Брошенная по таймауту операция дорабатывает позже, поэтому читает и пишет в свой буфер, а не в буфер вызывающего.
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var own = new byte[buffer.Length];
        var read = await gate.RunAsync(() => inner.Read(own, 0, own.Length), cancellationToken);
        own.AsSpan(0, read).CopyTo(buffer.Span);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var own = buffer.ToArray();
        await gate.RunAsync(() => inner.Write(own, 0, own.Length), cancellationToken);
        _length = Math.Max(_length, inner.Position);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        gate.RunAsync(inner.Flush, cancellationToken);

    public override void Flush() => FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

    public override async ValueTask DisposeAsync()
    {
        if (!_innerDisposed)
        {
            _innerDisposed = true;
            await gate.RunAsync(inner.Dispose);
        }

        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_innerDisposed)
        {
            _innerDisposed = true;
            gate.RunAsync(inner.Dispose).GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}
