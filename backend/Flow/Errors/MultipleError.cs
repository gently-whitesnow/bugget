using System.Collections.Generic;

namespace Flow.Errors;

public record MultipleError(string Error, string Reason, IEnumerable<MultipleErrorElement> ErrorList);
public record MultipleErrorElement(string Error, string Reason);
