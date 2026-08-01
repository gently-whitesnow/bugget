namespace Bugget.Application.Ports;

/// <summary>
/// Opaque транзакционный scope. BO-слой получает его от <see cref="IUnitOfWork"/>
/// и передаёт в DA-методы / <c>IDomainEventPublisher</c>, не зная о Npgsql-типах.
/// Внутренние Npgsql-объекты доступны только DA-проекту.
/// </summary>
public interface ITransactionScope
{
}
