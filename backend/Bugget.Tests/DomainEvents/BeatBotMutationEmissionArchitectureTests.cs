using System.IO;
using System.Linq;
using System.Reflection;
using Bugget.BO.DomainEvents;
using Bugget.BO.Ports;
using Bugget.BO.Services.Bugs;
using Bugget.BO.Services.Comments;
using Npgsql;

namespace Bugget.Tests.DomainEvents;

/// <summary>
/// Architecture-guard: сервисы, делающие beta-relevant mutations, обязаны зависеть от
/// `IDomainEventPublisher` (эмиссия события) и `IUnitOfWork` (атомарность mutation+event).
/// Прямые Npgsql-зависимости (`NpgsqlDataSource`/`NpgsqlConnection`/`NpgsqlTransaction`) в
/// конструкторах BO-сервисов запрещены: BO задаёт границы транзакции через UoW, не работает
/// с DA-specifics напрямую.
/// </summary>
public class BetaBotMutationEmissionArchitectureTests
{
    [Theory(DisplayName = "State-changing сервис зависит от IDomainEventPublisher и IUnitOfWork")]
    [InlineData(typeof(BugsService))]
    [InlineData(typeof(CommentsService))]
    public void ServiceConstructor_HasDomainEventPublisherAndUnitOfWork(Type serviceType)
    {
        var ctor = serviceType.GetConstructors().Single();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(IDomainEventPublisher), paramTypes);
        Assert.Contains(typeof(IUnitOfWork), paramTypes);
    }

    [Theory(DisplayName = "BO-сервис не зависит напрямую от Npgsql-типов: транзакцию открывает UoW, не сам сервис")]
    [InlineData(typeof(BugsService))]
    [InlineData(typeof(CommentsService))]
    public void ServiceConstructor_DoesNotDependOnNpgsqlTypes(Type serviceType)
    {
        var ctor = serviceType.GetConstructors().Single();
        var npgsqlParams = ctor.GetParameters()
            .Where(p => p.ParameterType.Namespace?.StartsWith("Npgsql", StringComparison.Ordinal) == true)
            .Select(p => $"{p.Name}: {p.ParameterType.FullName}")
            .ToArray();

        Assert.Empty(npgsqlParams);
    }

    [Fact(DisplayName = "Bugget.BO/** не импортирует Npgsql: DA-specifics не должны течь в BO-слой")]
    public void BoLayer_DoesNotImportNpgsql()
    {
        var boRoot = LocateBoSourceRoot();
        var offenders = Directory.EnumerateFiles(boRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllLines(path)
                .Any(line => line.TrimStart().StartsWith("using Npgsql", StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(boRoot, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string LocateBoSourceRoot()
    {
        // Тесты исполняются из Bugget.Tests/bin/<Configuration>/<tfm>/. Поднимаемся к solution-root и идём в Bugget.BO.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Bugget.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var bo = Path.Combine(dir!.FullName, "Bugget.BO");
        Assert.True(Directory.Exists(bo), $"Bugget.BO source root not found at {bo}");
        return bo;
    }
}
