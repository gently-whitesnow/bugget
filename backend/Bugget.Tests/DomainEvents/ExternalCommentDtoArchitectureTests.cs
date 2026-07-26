using System.Linq;
using Bugget.Entities.DTO.Internal;

namespace Bugget.Tests.DomainEvents;

/// <summary>
/// I-1 invariant: response DTO <see cref="InternalExternalCommentItemDto"/> не проектирует
/// поле <c>audience</c> — внешний потребитель (beta-bot) не должен даже знать про этот
/// атрибут. Compile-time проверка через reflection.
/// </summary>
public class ExternalCommentDtoArchitectureTests
{
    [Fact(DisplayName = "InternalExternalCommentItemDto не содержит свойства Audience (I-1)")]
    public void Dto_DoesNotExposeAudience()
    {
        var props = typeof(InternalExternalCommentItemDto).GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("Audience", props, StringComparer.OrdinalIgnoreCase);
    }
}
