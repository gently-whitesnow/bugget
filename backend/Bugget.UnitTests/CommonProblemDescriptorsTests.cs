using Bugget.Api.Http;

namespace Bugget.UnitTests;

public sealed class CommonProblemDescriptorsTests
{
    [Theory]
    [InlineData(400, "bad_request")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(405, "method_not_allowed")]
    [InlineData(415, "unsupported_media_type")]
    [InlineData(500, "internal_server_error")]
    public void Framework_statuses_resolve_to_catalog_descriptors(int status, string code)
    {
        var descriptor = CommonProblemDescriptors.ForStatus(status);

        Assert.Equal(code, descriptor.Code);
        Assert.Equal(status, descriptor.Status);
    }

    /// <summary>
    /// Незнакомый статус — единственный случай, когда код выводится, а не берётся из
    /// каталога. Ответа без кода не бывает: разбирать его клиенту было бы нечем.
    /// </summary>
    [Fact]
    public void Unknown_status_still_gets_a_stable_code()
    {
        var descriptor = CommonProblemDescriptors.ForStatus(418);

        Assert.Equal("http_418", descriptor.Code);
        Assert.Equal(418, descriptor.Status);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Title));
    }
}
