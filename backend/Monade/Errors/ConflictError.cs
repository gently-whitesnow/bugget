using JetBrains.Annotations;

namespace Monade.Errors;

[PublicAPI]
public record ConflictError(string Error, string Reason) : Error;
