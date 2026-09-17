namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Второй нарушитель — без какого-либо узнаваемого суффикса. Правило смотрит на сборку
/// и конструктор, а не на имя типа.
/// </summary>
public sealed record LeakingRealtimePublisher(
    global::Bugget.Application.Services.Reports.ReportsService Service);
