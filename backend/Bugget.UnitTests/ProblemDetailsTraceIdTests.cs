using System.Diagnostics;
using Bugget.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.UnitTests;

/// <summary>
/// Приоритет источников корреляционного идентификатора и его fallback вынесены отдельно от
/// остальных проверок фабрики: у них общая подготовка Activity и свой инвариант — traceId
/// обязателен и обязан совпадать с тем, что видит остальной контекст запроса.
/// </summary>
public sealed class ProblemDetailsTraceIdTests
{
    [Fact]
    public void Server_error_generates_non_empty_trace_id_when_activity_and_trace_identifier_are_absent()
    {
        var previousActivity = Activity.Current;
        try
        {
            Activity.Current = null;
            var context = new DefaultHttpContext { TraceIdentifier = "" };

            var problem = CreateServerErrorProblem(context);

            var traceId = Assert.IsType<string>(problem.Extensions["traceId"]);
            Assert.False(string.IsNullOrWhiteSpace(traceId));
            // Ответ и дальнейшая корреляция контекста обязаны ссылаться на один идентификатор.
            Assert.Equal(traceId, context.TraceIdentifier);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    /// <summary>
    /// Fallback — только для пустых источников: непустой TraceIdentifier остаётся и в ответе,
    /// и в контексте, иначе голый контекст получал бы новый id на каждой ошибке.
    /// </summary>
    [Fact]
    public void Trace_identifier_is_kept_as_is_when_it_is_not_empty()
    {
        var previousActivity = Activity.Current;
        try
        {
            Activity.Current = null;
            var context = new DefaultHttpContext { TraceIdentifier = "existing-trace-identifier" };

            var problem = CreateServerErrorProblem(context);

            Assert.Equal("existing-trace-identifier", problem.Extensions["traceId"]);
            Assert.Equal("existing-trace-identifier", context.TraceIdentifier);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    /// <summary>
    /// В настоящем пайплайне запрос идёт под Activity, и корреляция строится по её id, а не по
    /// TraceIdentifier: fallback — это ветка для голого контекста, а не основной путь.
    /// </summary>
    [Fact]
    public void Trace_id_comes_from_the_current_activity_when_there_is_one()
    {
        using var activity = new Activity("problem-details-test").Start();
        var context = new DefaultHttpContext();

        var problem = CreateServerErrorProblem(context);

        Assert.Equal(activity.Id, problem.Extensions["traceId"]);
        Assert.NotEqual(context.TraceIdentifier, problem.Extensions["traceId"]);
    }

    private static ProblemDetails CreateServerErrorProblem(HttpContext context) =>
        Assert.IsType<ProblemDetails>(
            ProblemDetailsFactory.Create(context, CommonProblemDescriptors.InternalServerError).Value);
}
