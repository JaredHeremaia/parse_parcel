using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shipping.Contracts;
using Shipping.Core.Model;
using Xunit;

namespace Shipping.Api.Tests;

public sealed class PackageEndpointsTests : IDisposable
{
    private readonly ShippingApiFactory _factory = new();
    private readonly HttpClient _client;

    public PackageEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Health_reports_ok()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_packages_returns_the_published_price_list()
    {
        var packageTypes = await _client.GetFromJsonAsync<List<PackageTypeResponse>>("/api/packages");

        Assert.NotNull(packageTypes);
        Assert.Equal(3, packageTypes.Count);

        var small = packageTypes.Single(p => p.Name == "Small");
        Assert.Equal(200, small.Dimensions.LengthMm);
        Assert.Equal(300, small.Dimensions.BreadthMm);
        Assert.Equal(150, small.Dimensions.HeightMm);
        Assert.Equal(5.00m, small.Cost);
        Assert.Equal(25m, small.MaxWeightKg);
    }

    [Fact]
    public async Task Get_packages_returns_them_cheapest_first()
    {
        var packageTypes = await _client.GetFromJsonAsync<List<PackageTypeResponse>>("/api/packages");

        Assert.Equal(
            new[] { "Small", "Medium", "Large" },
            packageTypes!.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Costs_are_plain_numbers_with_no_currency()
    {
        var json = await _client.GetStringAsync("/api/packages");

        Assert.Contains("\"cost\":5.00", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NZD", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Small")]
    [InlineData("small")]
    [InlineData("SMALL")]
    public async Task Get_package_by_name_is_case_insensitive(string name)
    {
        var packageType = await _client.GetFromJsonAsync<PackageTypeResponse>($"/api/packages/{name}");

        Assert.Equal("Small", packageType!.Name);
        Assert.Equal(StandardPackageTypes.SmallId, packageType.Id);
    }

    [Fact]
    public async Task Get_package_by_id_returns_that_package()
    {
        var packageType = await _client
            .GetFromJsonAsync<PackageTypeResponse>($"/api/packages/{StandardPackageTypes.LargeId}");

        Assert.Equal("Large", packageType!.Name);
        Assert.Equal(8.50m, packageType.Cost);
    }

    [Fact]
    public async Task Get_package_that_does_not_exist_returns_404_with_problem_details()
    {
        var response = await _client.GetAsync("/api/packages/enormous");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(404, problem!.Status);
        Assert.Contains("enormous", problem.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_adds_a_package_type_and_returns_its_location()
    {
        var request = new PackageTypeRequest("Extra Large", 500, 700, 300, 12.50m);

        var response = await _client.PostAsJsonAsync("/api/packages", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<PackageTypeResponse>();
        Assert.Equal("Extra Large", created!.Name);
        Assert.Equal($"/api/packages/{created.Id}", response.Headers.Location!.ToString());

        var fetched = await _client.GetFromJsonAsync<PackageTypeResponse>(response.Headers.Location);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task A_newly_added_package_type_can_be_quoted_against()
    {
        await _client.PostAsJsonAsync("/api/packages", new PackageTypeRequest("Huge", 900, 900, 900, 20m));

        var response = await _client.PostAsJsonAsync("/api/quotes", new QuoteRequest(850, 850, 850, 10m));
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();

        Assert.Equal("Huge", quote!.PackageType);
    }

    [Fact]
    public async Task Post_with_a_duplicate_name_returns_409()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/packages",
            new PackageTypeRequest("small", 10, 10, 10, 1m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_invalid_values_returns_400_listing_every_problem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/packages",
            new PackageTypeRequest(" ", -5, null, 0, -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(problem!.Errors);
        Assert.Equal(5, problem.Errors!.Count);
    }

    [Fact]
    public async Task Post_with_malformed_json_returns_400_rather_than_500()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/packages", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_applies_every_field_it_is_given()
    {
        var request = new PackageTypePatchRequest("Small", 210, 310, 160, 5.50m);

        var response = await PatchAsync(StandardPackageTypes.SmallId, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<PackageTypeResponse>();
        Assert.Equal(5.50m, updated!.Cost);
        Assert.Equal(210, updated.Dimensions.LengthMm);

        var fetched = await _client.GetFromJsonAsync<PackageTypeResponse>("/api/packages/small");
        Assert.Equal(5.50m, fetched!.Cost);
    }

    [Fact]
    public async Task Patch_leaves_out_fields_alone()
    {
        var response = await PatchAsync(
            StandardPackageTypes.SmallId,
            new PackageTypePatchRequest(Cost: 6.25m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<PackageTypeResponse>();

        Assert.Equal(6.25m, updated!.Cost);
        Assert.Equal("Small", updated.Name);
        Assert.Equal(200, updated.Dimensions.LengthMm);
        Assert.Equal(300, updated.Dimensions.BreadthMm);
        Assert.Equal(150, updated.Dimensions.HeightMm);
    }

    [Fact]
    public async Task Patch_can_change_one_side_without_the_others()
    {
        var response = await PatchAsync(
            StandardPackageTypes.SmallId,
            new PackageTypePatchRequest(LengthMm: 250));

        var updated = await response.Content.ReadFromJsonAsync<PackageTypeResponse>();

        Assert.Equal(250, updated!.Dimensions.LengthMm);
        Assert.Equal(300, updated.Dimensions.BreadthMm);
        Assert.Equal(5m, updated.Cost);
    }

    [Fact]
    public async Task Patch_with_an_empty_body_returns_400()
    {
        var response = await PatchAsync(StandardPackageTypes.SmallId, new PackageTypePatchRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains("at least one", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Patch_against_an_unknown_id_returns_404()
    {
        var response = await PatchAsync(Guid.NewGuid(), new PackageTypePatchRequest(Name: "Nope"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_that_takes_another_package_types_name_returns_409()
    {
        var response = await PatchAsync(
            StandardPackageTypes.SmallId,
            new PackageTypePatchRequest(Name: "Medium"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Patch_with_invalid_values_returns_400()
    {
        var response = await PatchAsync(
            StandardPackageTypes.SmallId,
            new PackageTypePatchRequest(LengthMm: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> PatchAsync(Guid id, PackageTypePatchRequest request)
        => _client.PatchAsJsonAsync($"/api/packages/{id}", request);

    [Fact]
    public async Task Delete_removes_the_package_type()
    {
        var response = await _client.DeleteAsync($"/api/packages/{StandardPackageTypes.LargeId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = await _client.GetAsync("/api/packages/large");
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task Delete_twice_returns_404_the_second_time()
    {
        await _client.DeleteAsync($"/api/packages/{StandardPackageTypes.LargeId}");

        var response = await _client.DeleteAsync($"/api/packages/{StandardPackageTypes.LargeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_with_something_that_is_not_an_id_does_not_match_the_route()
    {
        var response = await _client.DeleteAsync("/api/packages/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Guards the JSON shape the CLI and any other client depend on.</summary>
    [Fact]
    public async Task The_response_uses_camel_case_field_names()
    {
        var json = await _client.GetStringAsync("/api/packages/small");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("maxWeightKg", out _));
        Assert.True(root.GetProperty("dimensions").TryGetProperty("lengthMm", out _));
    }
}
