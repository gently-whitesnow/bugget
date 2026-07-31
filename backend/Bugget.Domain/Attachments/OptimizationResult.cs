namespace Bugget.Domain.Attachments;

public record OptimizationResult(
    string StorageKey,
    string MimeType,
    string FileName,
    long LengthBytes,
    bool IsGzipCompressed,
    bool HasPreview,
    long PreviewLengthBytes
);
