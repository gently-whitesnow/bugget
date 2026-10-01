using System.Xml.Linq;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Один проект решения: как он объявлен в своём .csproj.
/// </summary>
/// <param name="Name">Имя проекта без расширения, оно же имя сборки: <c>Bugget.Application</c>.</param>
/// <param name="Sdk">Значение атрибута Sdk: <c>Microsoft.NET.Sdk</c> или <c>Microsoft.NET.Sdk.Web</c>.</param>
/// <param name="ProjectReferences">Имена проектов из ProjectReference — прямые рёбра графа.</param>
/// <param name="PackageReferences">Имена пакетов из PackageReference — прямые внешние зависимости.</param>
/// <param name="IsTestProject">Проект помечен IsTestProject.</param>
public sealed record ProjectNode(
    string Name,
    string Sdk,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences,
    bool IsTestProject);
