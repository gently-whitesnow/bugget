using System.Reflection;
using FluentAssertions;
using LegacyAlias = Bugget.Architecture.Tests.PersistenceFixtures.DbModels.LegacyRow;

namespace Bugget.Architecture.Tests
{
    /// <summary>
    /// Проверяет фактические CLR-сигнатуры портов. Исходный alias здесь уже разрешён
    /// компилятором, поэтому проверку нельзя обойти переименованием using-директивы.
    /// </summary>
    public sealed class PortContractRulesTests
    {
        private static readonly Assembly[] ApplicationAssemblies =
        [
            Quartet.ApplicationAsm,
        ];

        [Fact(DisplayName = "Публичные интерфейсы прикладного слоя не раскрывают persistence-типы в сигнатурах")]
        public void Ports_do_not_expose_persistence_types()
        {
            var ports = ApplicationAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsInterface && type.IsPublic);
            var leaks = FindPersistenceLeaks(ports);

            leaks.Should().BeEmpty(
                "порт — контракт прикладного слоя и не должен принимать или возвращать типы из Bugget.Infrastructure, " +
                "Npgsql/Dapper/System.Data.Common либо legacy namespace DbModels. Алиас типа " +
                "не является обходом: reflection видит фактический CLR-тип. Утечки: {0}",
                string.Join("; ", leaks));
        }

        [Fact(DisplayName = "Проверка портов краснеет на persistence-типе, скрытом алиасом")]
        public void Port_rule_is_provably_red_for_an_aliased_legacy_type()
        {
            FindPersistenceLeaks([typeof(AliasedLeakingPort)])
                .Should().ContainSingle()
                .Which.Should().Contain("PersistenceFixtures.DbModels.LegacyRow");
        }

        private static string[] FindPersistenceLeaks(IEnumerable<Type> ports)
        {
            return ports
                .SelectMany(port => port.GetMethods().SelectMany(method =>
                    method.GetParameters()
                        .Select(parameter => (Member: $"{port.FullName}.{method.Name}({parameter.Name})", Type: parameter.ParameterType))
                        .Append((Member: $"{port.FullName}.{method.Name} return", Type: method.ReturnType))))
                .SelectMany(item => ExpandType(item.Type)
                    .Where(IsPersistenceType)
                    .Select(type => $"{item.Member} → {type.FullName}"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<Type> ExpandType(Type type)
        {
            yield return type;

            if (type.HasElementType && type.GetElementType() is { } elementType)
            {
                foreach (var nested in ExpandType(elementType))
                {
                    yield return nested;
                }
            }

            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var nested in ExpandType(argument))
                {
                    yield return nested;
                }
            }
        }

        private static bool IsPersistenceType(Type type)
        {
            var assembly = type.Assembly.GetName().Name ?? string.Empty;
            var ns = type.Namespace ?? string.Empty;

            return assembly.Equals("Bugget.Infrastructure", StringComparison.Ordinal)
                   || assembly is "Npgsql" or "Dapper" or "System.Data.Common"
                   || ns.Contains(".DbModels", StringComparison.Ordinal);
        }

        private interface AliasedLeakingPort
        {
            Task<LegacyAlias> ReadAsync();
        }
    }
}

namespace Bugget.Architecture.Tests.PersistenceFixtures.DbModels
{
    public sealed class LegacyRow;
}
