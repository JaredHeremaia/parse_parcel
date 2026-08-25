using System.Net;
using System.Net.Http.Json;
using Shipping.Contracts;
using Xunit;

namespace Shipping.Api.Tests;

public sealed class QuoteEndpointsTests : IDisposable
{
    private readonly ShippingApiFactory _factory = new();
    private readonly HttpClient _client;

    public QuoteEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData(100, 100, 100, "Small", 5.00)]
    [InlineData(200, 300, 150, "Small", 5.00)]
    [InlineData(201, 300, 150, "Medium", 7.50)]
    [InlineData(300, 400, 200, "Medium", 7.50)]
    [InlineData(301, 400, 200, "Large", 8.50)]
    [InlineData(400, 600, 250, "Large", 8.50)]
    public async Task A_package_that_fits_gets_a_type_and_a_cost(
        int length,
        int breadth,
        int height,
        string expectedType,
        decimal expectedCost)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteRequest(length, breadth, height, 5m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.Equal(expectedType, quote!.PackageType);
        Assert.Equal(expectedCost, quote.Cost);
        Assert.Equal(5m, quote.WeightKg);
    }

    [Fact]
    public async Task Exactly_the_weight_limit_is_still_shippable()
    {
        var response = await _client.PostAsJsonAsync("/api/quotes", new QuoteRequest(100, 100, 100, 25m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(25.01)]
    [InlineData(30)]
    public async Task An_overweight_package_gets_no_solution(decimal weightKg)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteRequest(100, 100, 100, weightKg));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("Overweight", problem!.Reason);
    }

    [Theory]
    [InlineData(401, 600, 250)]
    [InlineData(400, 601, 250)]
    [InlineData(400, 600, 251)]
    public async Task An_oversized_package_gets_no_solution(int length, int breadth, int height)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteRequest(length, breadth, height, 1m));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("Oversized", problem!.Reason);
    }

    [Fact]
    public async Task A_package_may_be_turned_to_fit_a_smaller_box()
    {
        var response = await _client.PostAsJsonAsync("/api/quotes", new QuoteRequest(300, 150, 200, 1m));

        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.Equal("Small", quote!.PackageType);
    }

    [Theory]
    [InlineData(0, 300, 150, 5)]
    [InlineData(-1, 300, 150, 5)]
    [InlineData(200, 300, 150, 0)]
    [InlineData(200, 300, 150, -5)]
    public async Task Nonsensical_input_is_rejected_as_a_bad_request(
        int length,
        int breadth,
        int height,
        decimal weightKg)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/quotes",
            new QuoteRequest(length, breadth, height, weightKg));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_fields_are_reported_individually()
    {
        var response = await _client.PostAsJsonAsync("/api/quotes", new QuoteRequest(null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(4, problem!.Errors!.Count);
    }

    [Fact]
    public async Task A_quote_echoes_the_package_it_was_asked_about()
    {
        var response = await _client.PostAsJsonAsync("/api/quotes", new QuoteRequest(150, 150, 100, 2.5m));

        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();

        Assert.Equal(150, quote!.Dimensions.LengthMm);
        Assert.Equal(150, quote.Dimensions.BreadthMm);
        Assert.Equal(100, quote.Dimensions.HeightMm);
        Assert.Equal(2.5m, quote.WeightKg);
        Assert.NotEqual(Guid.Empty, quote.PackageTypeId);
    }
}
