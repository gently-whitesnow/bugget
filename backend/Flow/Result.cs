using JetBrains.Annotations;

namespace Flow;

[PublicAPI]
public record struct ResultStruct
{
    public Error? Error { get; init; }

    public bool IsSuccess => Error == null;
    public bool HasError => !IsSuccess;

    public ResultStruct(Error error)
    {
        Error = error;
    }

    public static readonly ResultStruct Success = new();
    public static implicit operator ResultStruct(Error error) => new(error);
}

[PublicAPI]
public record struct ResultStruct<TValue>
{
    public bool IsSuccess => Error == null;
    public bool HasError => !IsSuccess;
    public TValue? Value { get; init; }
    public Error? Error { get; init; }

    public ResultStruct(TValue value)
    {
        Value = value;
    }

    public ResultStruct(Error error)
    {
        Error = error;
    }

    public static implicit operator ResultStruct<TValue>(TValue value) => new(value);
    public static implicit operator ResultStruct<TValue>(Error error) => new(error);
}
