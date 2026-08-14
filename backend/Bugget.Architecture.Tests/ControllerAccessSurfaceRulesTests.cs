using System.Reflection;
using Bugget.Api.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Кто закрывает каждый контроллер <c>Bugget.Api</c>. В одном процессе живут три модуля
/// с разными схемами: reports закрывает конвенция
/// <see cref="ReportsModuleAuthorizationConvention"/>, users и authorization — свои
/// атрибуты. Пока модули были отдельными сборками, границей конвенции была сборка;
/// после слияния в квартет она молча накрыла анонимный OIDC-callback, и новые
/// пользователи перестали заводиться. Правило ниже держит обе стороны этой границы:
/// контроллер без собственной авторизации обязан либо попадать в модуль reports, либо
/// стоять в поимённом списке анонимной поверхности — то есть в диффе.
/// </summary>
public class ControllerAccessSurfaceRulesTests
{
    /// <summary>
    /// Анонимная поверхность: контроллеры, которым аутентификация не нужна по замыслу.
    /// Список поимённый, потому что «нет атрибута» и «доступ открыт осознанно» снаружи
    /// выглядят одинаково — а цена ошибки разная в обе стороны.
    /// </summary>
    private static readonly string[] AnonymousSurface =
    [
        // Вызывает oauth2-proxy до того, как пользователь появился в базе; доверие даёт
        // токен провайдера, который контроллер валидирует сам.
        "Bugget.Api.Authorization.Oidc.OidcController",

        // Вход fake-провайдера: регистрируется только в Development, в Production
        // маршрута нет (FakeLoginEnvironmentBoundaryTests).
        "Bugget.Api.Authorization.Fake.FakeController",

        // Точка auth_request для nginx: сама схема аутентификации и есть её проверка.
        "Bugget.Api.Authorization.Controllers.InternalAuthController",
    ];

    [Fact(DisplayName = "Каждый контроллер закрыт конвенцией reports, своим атрибутом или списком анонимных")]
    public void Every_controller_declares_who_closes_it()
    {
        var unguarded = FindUnguardedControllers(Quartet.ApiAsm);

        unguarded.Should().BeEmpty(
            "контроллер не закрыт ничем: он вне модуля reports, не объявляет авторизацию " +
            "атрибутом и не перечислен в списке анонимной поверхности. Либо повесь " +
            "[Auth]/[JwtAuth]/[InternalAuth], либо добавь строку в AnonymousSurface вместе " +
            "с причиной — молчаливой анонимности тут быть не должно. Нарушители: {0}",
            string.Join(", ", unguarded));
    }

    [Fact(DisplayName = "Конвенция reports накрывает только контроллеры модуля reports")]
    public void Reports_convention_covers_only_reports_module()
    {
        var covered = Controllers(Quartet.ApiAsm)
            .Where(ReportsModuleAuthorizationConvention.BelongsToReportsModule)
            .Select(type => type.FullName!)
            .ToArray();

        covered.Should().NotBeEmpty("иначе правило проверяет пустое множество");
        covered.Should().OnlyContain(
            name => name.StartsWith("Bugget.Api.Controllers.", StringComparison.Ordinal),
            "конвенция вешает схему headers, которой у модулей users и authorization нет: " +
            "их контроллеры она закрывать не должна");
    }

    [Fact(DisplayName = "OIDC-callback остаётся анонимным")]
    public void Oidc_callback_stays_anonymous()
    {
        var callback = typeof(global::Bugget.Api.Authorization.Oidc.OidcController)
            .GetMethod("CallbackAsync", BindingFlags.Public | BindingFlags.Instance);

        callback.Should().NotBeNull("маршрут переименован — правило потеряло цель");

        ReportsModuleAuthorizationConvention
            .BelongsToReportsModule(typeof(global::Bugget.Api.Authorization.Oidc.OidcController))
            .Should().BeFalse("конвенция навязала бы схему headers, а заголовков у callback нет");

        callback!.GetCustomAttributes(inherit: true)
            .OfType<IAllowAnonymous>()
            .Should().NotBeEmpty(
                "новый пользователь заводится именно здесь: под обязательной аутентификацией " +
                "callback отвечает 401 до тела экшена, и вход ломается у всех, кого ещё нет в базе");
    }

    [Fact(DisplayName = "Правило краснеет на контроллере без всякой защиты")]
    public void Rule_is_provably_red_for_an_unguarded_controller()
    {
        FindUnguardedControllers(typeof(ControllerAccessSurfaceRulesTests).Assembly)
            .Should().Contain(typeof(AccessSurfaceFixtures.UnguardedController).FullName!);
    }

    [Fact(DisplayName = "Правило пропускает контроллер с собственным атрибутом авторизации")]
    public void Rule_is_provably_green_for_a_controller_with_its_own_attribute()
    {
        FindUnguardedControllers(typeof(ControllerAccessSurfaceRulesTests).Assembly)
            .Should().NotContain(typeof(AccessSurfaceFixtures.GuardedController).FullName!);
    }

    /// <summary>
    /// Контроллеры сборки, за которыми не стоит ни конвенция reports, ни собственный
    /// атрибут авторизации, ни строка в списке анонимных. Отдельная функция, а не тело
    /// теста: ту же проверку прогоняет доказательство красноты на фикстурах.
    /// </summary>
    private static string[] FindUnguardedControllers(Assembly assembly) =>
        [
            .. Controllers(assembly)
                .Where(type => !ReportsModuleAuthorizationConvention.BelongsToReportsModule(type))
                .Where(type => !DeclaresAuthorization(type))
                .Select(type => type.FullName!)
                .Where(name => !AnonymousSurface.Contains(name, StringComparer.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
        ];

    private static IEnumerable<Type> Controllers(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type));

    /// <summary>
    /// Авторизация объявлена самим контроллером: атрибут на типе, на его базе или на
    /// любом действии. Смотрим на <see cref="IAuthorizeData"/>, а не на конкретные
    /// атрибуты модулей: у каждого из них своя схема, а требование тут одно.
    /// </summary>
    private static bool DeclaresAuthorization(Type type) =>
        type.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any()
        || type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any());
}
