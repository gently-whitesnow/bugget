using System.Linq;
using System.Reflection;
using Bugget.BO.DomainEvents;
using Bugget.DA.Transactions;
using Bugget.Entities.Constants;

namespace Bugget.Tests.DomainEvents;

public class DomainEventPublisherArchitectureTests
{
    [Fact(DisplayName = "IDomainEventPublisher.PublishAsync принимает ITransactionScope, не Npgsql-типы — BO не видит DA-specifics")]
    public void PublishAsync_Signature_RequiresTransactionScope()
    {
        // Перегрузка со scope — для атомарной с UPDATE эмиссии (read-model events).
        var methods = typeof(IDomainEventPublisher)
            .GetMethods()
            .Where(m => m.Name == nameof(IDomainEventPublisher.PublishAsync))
            .ToArray();
        Assert.NotEmpty(methods);

        var txMethod = methods.SingleOrDefault(m =>
            m.GetParameters().Any(p => p.ParameterType == typeof(ITransactionScope)));
        Assert.NotNull(txMethod);

        // Запрещаем Npgsql-типы в публичной сигнатуре publisher'а: BO-контракт должен быть DA-агностичным.
        foreach (var method in methods)
        {
            var npgsqlParams = method.GetParameters()
                .Where(p => p.ParameterType.Namespace?.StartsWith("Npgsql", StringComparison.Ordinal) == true)
                .ToArray();
            Assert.Empty(npgsqlParams);
        }

        var nullableCtx = typeof(IDomainEventPublisher).CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
        Assert.NotNull(nullableCtx);
        Assert.Equal((byte)1, (byte)nullableCtx!.ConstructorArguments[0].Value!);
    }

    [Fact(DisplayName = "PublishAsync бросает при null scope — защита на runtime")]
    public async Task PublishAsync_Throws_WhenScopeNull()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable(EnvironmentConstants.PostgresConnectionString);
        Environment.SetEnvironmentVariable(
            EnvironmentConstants.PostgresConnectionString,
            previousConnectionString ?? "Host=localhost;Username=postgres;Database=postgres");

        try
        {
            var publisher = new DomainEventPublisher(new Bugget.DA.Postgres.DomainEventsDbClient());

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                publisher.PublishAsync(
                    new Bugget.Entities.DbModels.DomainEvents.DomainEventDbModel
                    {
                        WorkspaceId = "ws",
                        AggregateType = "bug",
                        AggregateId = "1",
                        EventType = "bugget.bug.created",
                        Payload = "{}",
                    },
                    scope: null!));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentConstants.PostgresConnectionString, previousConnectionString);
        }
    }
}
