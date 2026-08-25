using System.Net;
using System.Text.Json;
using Shipping.Cli;
using Xunit;

namespace Shipping.Cli.Tests;

public class CommandRunnerTests
{
    private const string SmallJson =
        """
        {
          "id": "11111111-1111-1111-1111-111111111111",
          "name": "Small",
          "dimensions": { "lengthMm": 200, "breadthMm": 300, "heightMm": 150, "volumeMm3": 9000000 },
          "cost": 5.00,
          "maxWeightKg": 25
        }
        """;

    private static async Task<(int ExitCode, string Output, StubHttpMessageHandler Handler)> RunAsync(
        string[] args,
        Func<HttpRequestMessage, string, HttpResponseMessage> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080/") };
        using var output = new StringWriter();

        var runner = new CommandRunner(new ShippingApiClient(http), output);
        var exitCode = await runner.RunAsync(CommandLineArgs.Parse(args));

        return (exitCode, output.ToString(), handler);
    }

    [Fact]
    public async Task No_arguments_prints_the_help_text()
    {
        var (exitCode, output, handler) = await RunAsync(
            [],
            (_, _) => throw new InvalidOperationException("the API should not be called"));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    public async Task Help_is_available_by_name_and_by_flag(string argument)
    {
        var (exitCode, output, _) = await RunAsync(
            [argument],
            (_, _) => throw new InvalidOperationException());

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("shipping quote", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_command_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(["teleport"], (_, _) => throw new InvalidOperationException()));

    [Fact]
    public async Task An_unknown_sub_command_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(["packages", "frobnicate"], (_, _) => throw new InvalidOperationException()));

    [Fact]
    public async Task Listing_packages_renders_a_table()
    {
        var (exitCode, output, handler) = await RunAsync(
            ["packages", "list"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, $"[{SmallJson}]"));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Small", output, StringComparison.Ordinal);
        Assert.Contains("5.00", output, StringComparison.Ordinal);
        Assert.Equal("/api/packages", handler.Requests.Single().Path);
    }

    [Fact]
    public async Task Packages_with_no_sub_command_lists_them()
    {
        var (exitCode, _, handler) = await RunAsync(
            ["packages"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, $"[{SmallJson}]"));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task Getting_a_package_by_name_requests_that_name()
    {
        var (exitCode, output, handler) = await RunAsync(
            ["packages", "get", "small"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, SmallJson));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("/api/packages/small", handler.Requests.Single().Path, StringComparison.Ordinal);
        Assert.Contains("200mm", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Getting_a_package_without_a_key_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(["packages", "get"], (_, _) => throw new InvalidOperationException()));

    [Fact]
    public async Task A_quote_is_reported_with_its_package_type_and_cost()
    {
        const string QuoteJson =
            """
            {
              "packageTypeId": "11111111-1111-1111-1111-111111111111",
              "packageType": "Small",
              "cost": 5.00,
              "dimensions": { "lengthMm": 200, "breadthMm": 300, "heightMm": 150, "volumeMm3": 9000000 },
              "weightKg": 5
            }
            """;

        var (exitCode, output, handler) = await RunAsync(
            ["quote", "--length", "200", "--breadth", "300", "--height", "150", "--weight", "5"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, QuoteJson));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Small", output, StringComparison.Ordinal);
        Assert.Contains("5.00", output, StringComparison.Ordinal);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/quotes", request.Path);
    }

    [Fact]
    public async Task No_packaging_solution_is_reported_with_its_own_exit_code()
    {
        const string ProblemJson =
            """
            {
              "title": "No packaging solution",
              "status": 422,
              "detail": "We cannot currently ship packages over 25kg (this one is 30kg).",
              "reason": "Overweight"
            }
            """;

        var (exitCode, output, _) = await RunAsync(
            ["quote", "--length", "100", "--breadth", "100", "--height", "100", "--weight", "30"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.UnprocessableEntity, ProblemJson));

        Assert.Equal(ExitCodes.NoPackagingSolution, exitCode);
        Assert.Contains("No packaging solution", output, StringComparison.Ordinal);
        Assert.Contains("25kg", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quote_missing_an_argument_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(["quote", "--length", "200"], (_, _) => throw new InvalidOperationException()));

    [Fact]
    public async Task An_api_failure_surfaces_the_servers_explanation()
    {
        const string ProblemJson =
            """
            { "title": "Not found", "status": 404, "detail": "No package type found for 'enormous'." }
            """;

        var error = await Assert.ThrowsAsync<ApiException>(() => RunAsync(
            ["packages", "get", "enormous"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.NotFound, ProblemJson)));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Contains("enormous", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_non_json_error_response_still_produces_a_readable_message()
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => RunAsync(
            ["packages", "list"],
            (_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("<html>gateway blew up</html>"),
            }));

        Assert.Contains("502", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adding_a_package_sends_every_field()
    {
        var (exitCode, _, handler) = await RunAsync(
            [
                "packages", "add",
                "--name", "Extra Large",
                "--length", "500",
                "--breadth", "700",
                "--height", "300",
                "--cost", "12.50",
            ],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Created, SmallJson));

        Assert.Equal(ExitCodes.Success, exitCode);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("Extra Large", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(500, body.RootElement.GetProperty("lengthMm").GetInt32());
        Assert.Equal(12.50m, body.RootElement.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Adding_a_package_without_all_the_fields_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(
            ["packages", "add", "--name", "Extra Large", "--length", "500"],
            (_, _) => throw new InvalidOperationException()));

    /// <summary>
    /// PUT replaces the whole resource, so the CLI reads the current values first and
    /// only overwrites what the user actually supplied.
    /// </summary>
    [Fact]
    public async Task Updating_one_field_keeps_the_rest_of_the_package_type()
    {
        var (exitCode, _, handler) = await RunAsync(
            ["packages", "update", "11111111-1111-1111-1111-111111111111", "--cost", "6.25"],
            (_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, SmallJson));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(2, handler.Requests.Count);

        var get = handler.Requests[0];
        var put = handler.Requests[1];

        Assert.Equal(HttpMethod.Get, get.Method);
        Assert.Equal(HttpMethod.Put, put.Method);

        using var body = JsonDocument.Parse(put.Body);
        Assert.Equal("Small", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(200, body.RootElement.GetProperty("lengthMm").GetInt32());
        Assert.Equal(300, body.RootElement.GetProperty("breadthMm").GetInt32());
        Assert.Equal(150, body.RootElement.GetProperty("heightMm").GetInt32());
        Assert.Equal(6.25m, body.RootElement.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Updating_nothing_at_all_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(
            ["packages", "update", "11111111-1111-1111-1111-111111111111"],
            (_, _) => throw new InvalidOperationException()));

    [Fact]
    public async Task Updating_without_an_id_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(
            ["packages", "update", "--cost", "6.25"],
            (_, _) => throw new InvalidOperationException()));

    [Fact]
    public async Task Deleting_sends_a_delete_for_that_id()
    {
        var id = Guid.NewGuid();

        var (exitCode, output, handler) = await RunAsync(
            ["packages", "delete", id.ToString()],
            (_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(id.ToString(), output, StringComparison.Ordinal);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/api/packages/{id}", request.Path);
    }

    [Fact]
    public async Task Deleting_something_that_is_not_an_id_is_a_usage_error()
        => await Assert.ThrowsAsync<CommandLineException>(
            () => RunAsync(
            ["packages", "delete", "not-a-guid"],
            (_, _) => throw new InvalidOperationException()));
}
