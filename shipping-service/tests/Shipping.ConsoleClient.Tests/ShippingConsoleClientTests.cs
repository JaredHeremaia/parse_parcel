using System.Net;
using Shipping.Contracts;
using Xunit;

namespace Shipping.ConsoleClient.Tests;

public class ShippingConsoleClientTests
{
    private static readonly IReadOnlyList<PackageTypeResponse> Catalogue =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Small",
            new DimensionsResponse(200, 300, 150, 9_000_000L), 5m, 25m),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Medium",
            new DimensionsResponse(300, 400, 200, 24_000_000L), 7.5m, 25m),
    ];

    [Fact]
    public async Task The_catalogue_is_listed_with_costs_to_the_cent()
    {
        var (client, output, _) = Build(RespondWith(
            StubHttpMessageHandler.Ok(Catalogue),
            StubHttpMessageHandler.Ok(Quote("Small", 5m))));

        await client.RunAsync([]);

        var text = output.ToString();

        Assert.Contains("Small", text, StringComparison.Ordinal);
        Assert.Contains("Medium", text, StringComparison.Ordinal);
        Assert.Contains("5.00 NZD", text, StringComparison.Ordinal);
        Assert.Contains("7.50 NZD", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quote_reports_the_package_type_and_the_cost()
    {
        var (client, output, _) = Build(RespondWith(
            StubHttpMessageHandler.Ok(Quote("Medium", 7.5m))));

        var exitCode = await client.RunAsync(["quote", "201", "300", "150", "5"]);

        var text = output.ToString();

        Assert.Equal(0, exitCode);
        Assert.Contains("Package type : Medium", text, StringComparison.Ordinal);
        Assert.Contains("Cost         : 7.50 NZD", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_dimensions_asked_for_are_the_ones_sent_to_the_api()
    {
        var handler = RespondWith(StubHttpMessageHandler.Ok(Quote("Medium", 7.5m)));

        var (client, _, _) = Build(handler);

        await client.RunAsync(["quote", "201", "302", "153", "6.5"]);

        var request = Assert.Single(handler.Requests);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/quotes", request.RequestUri!.AbsolutePath);
        Assert.Contains("\"lengthMm\":201", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"breadthMm\":302", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"heightMm\":153", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"weightKg\":6.5", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_posts_the_package_type_and_reports_what_was_created()
    {
        var created = new PackageTypeResponse(
            Guid.Parse("44444444-4444-4444-4444-444444444444"), "Extra Large",
            new DimensionsResponse(500, 700, 300, 105_000_000L), 12.5m, 25m);

        var handler = RespondWith(StubHttpMessageHandler.Created(created));
        var (client, output, _) = Build(handler);

        var exitCode = await client.RunAsync(["add", "Extra Large", "500", "700", "300", "12.50"]);

        var request = Assert.Single(handler.Requests);

        Assert.Equal(0, exitCode);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/packages", request.RequestUri!.AbsolutePath);
        Assert.Contains("\"name\":\"Extra Large\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"cost\":12.50", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("Added 'Extra Large'.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("44444444-4444-4444-4444-444444444444", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_patches_the_id_it_was_given()
    {
        var updated = new PackageTypeResponse(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "Small",
            new DimensionsResponse(200, 300, 150, 9_000_000L), 6m, 25m);

        var handler = RespondWith(StubHttpMessageHandler.Ok(updated));
        var (client, output, _) = Build(handler);

        var exitCode = await client.RunAsync(
            ["update", "11111111-1111-1111-1111-111111111111", "--cost", "6.00"]);

        var request = Assert.Single(handler.Requests);

        Assert.Equal(0, exitCode);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/api/packages/11111111-1111-1111-1111-111111111111", request.RequestUri!.AbsolutePath);
        Assert.Contains("Updated 'Small'.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_sends_only_the_options_that_were_given()
    {
        var updated = new PackageTypeResponse(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "Small",
            new DimensionsResponse(200, 300, 150, 9_000_000L), 6m, 25m);

        var handler = RespondWith(StubHttpMessageHandler.Ok(updated));
        var (client, _, _) = Build(handler);

        await client.RunAsync(["update", "11111111-1111-1111-1111-111111111111", "--cost", "6.00"]);

        var body = handler.Bodies[0];

        // The fields not asked for are absent, so the API leaves them alone.
        Assert.Contains("\"cost\":6.00", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"lengthMm\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"breadthMm\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"heightMm\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_sends_a_delete_and_reads_no_body_back()
    {
        var handler = RespondWith(StubHttpMessageHandler.NoContent());
        var (client, output, _) = Build(handler);

        var exitCode = await client.RunAsync(["delete", "11111111-1111-1111-1111-111111111111"]);

        var request = Assert.Single(handler.Requests);

        Assert.Equal(0, exitCode);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/packages/11111111-1111-1111-1111-111111111111", request.RequestUri!.AbsolutePath);
        Assert.Contains("Deleted 11111111-1111-1111-1111-111111111111.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_duplicate_name_exits_two_and_reports_the_conflict()
    {
        var (client, _, error) = Build(RespondWith(
            StubHttpMessageHandler.Problem(
                HttpStatusCode.Conflict,
                new ApiErrorResponse(
                    "Conflict", "A package type named 'Small' already exists.", 409, null, null))));

        var exitCode = await client.RunAsync(["add", "Small", "200", "300", "150", "5.00"]);

        // The API answered and refused, which is different from never reaching it.
        Assert.Equal(2, exitCode);
        Assert.Contains("409 Conflict", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleting_something_already_gone_exits_two()
    {
        var (client, _, error) = Build(RespondWith(
            StubHttpMessageHandler.Problem(
                HttpStatusCode.NotFound,
                new ApiErrorResponse("Not found", "No package type found with id '...'.", 404, null, null))));

        var exitCode = await client.RunAsync(["delete", "11111111-1111-1111-1111-111111111111"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("404 Not found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_command_shows_the_usage_and_never_calls_the_api()
    {
        var handler = RespondWith(StubHttpMessageHandler.Ok(Catalogue));
        var (client, _, error) = Build(handler);

        var exitCode = await client.RunAsync(["destroy", "everything"]);

        Assert.Equal(1, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Contains("Unknown command 'destroy'", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Help_prints_the_usage_without_calling_the_api()
    {
        var handler = RespondWith(StubHttpMessageHandler.Ok(Catalogue));
        var (client, output, _) = Build(handler);

        var exitCode = await client.RunAsync(["help"]);

        Assert.Equal(0, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Contains(CommandLine.Usage, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_shows_the_catalogue_without_quoting()
    {
        var handler = RespondWith(StubHttpMessageHandler.Ok(Catalogue));
        var (client, output, _) = Build(handler);

        var exitCode = await client.RunAsync(["list"]);

        var request = Assert.Single(handler.Requests);

        Assert.Equal(0, exitCode);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.DoesNotContain("Quoting", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Overweight", "We cannot currently ship packages over 25kg (this one is 26kg).")]
    [InlineData("Oversized", "No package type can hold a package measuring 5000x5000x5000mm.")]
    public async Task A_package_we_cannot_ship_is_reported_with_its_reason(string reason, string detail)
    {
        var (client, output, _) = Build(RespondWith(
            StubHttpMessageHandler.Ok(Catalogue),
            StubHttpMessageHandler.Problem(
                HttpStatusCode.UnprocessableEntity,
                new ApiErrorResponse("No packaging solution", detail, 422, reason, null))));

        var exitCode = await client.RunAsync([]);

        var text = output.ToString();

        // A package we cannot ship is a normal answer, not a failure of the run.
        Assert.Equal(0, exitCode);
        Assert.Contains($"No packaging solution ({reason})", text, StringComparison.Ordinal);
        Assert.Contains(detail, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_request_lists_every_problem_on_the_error_writer()
    {
        var (client, _, error) = Build(RespondWith(
            StubHttpMessageHandler.Ok(Catalogue),
            StubHttpMessageHandler.Problem(
                HttpStatusCode.BadRequest,
                new ApiErrorResponse(
                    "Invalid request",
                    "lengthMm must be greater than zero.",
                    400,
                    null,
                    ["lengthMm must be greater than zero.", "weightKg is required."]))));

        await client.RunAsync([]);

        var text = error.ToString();

        Assert.Contains("400 Invalid request", text, StringComparison.Ordinal);
        Assert.Contains("- lengthMm must be greater than zero.", text, StringComparison.Ordinal);
        Assert.Contains("- weightKg is required.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_address_is_shown_as_typed_without_a_trailing_slash()
    {
        var (client, output, _) = Build(RespondWith(
            StubHttpMessageHandler.Ok(Catalogue),
            StubHttpMessageHandler.Ok(Quote("Small", 5m))));

        await client.RunAsync([]);

        Assert.Contains("at http://localhost:5080\n", output.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_api_explains_how_to_start_it()
    {
        var (client, _, error) = Build(StubHttpMessageHandler.Unreachable());

        var exitCode = await client.RunAsync([]);

        var text = error.ToString();

        Assert.Equal(1, exitCode);
        Assert.Contains("Could not reach the API", text, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project src/Shipping.Api", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bad_arguments_show_the_usage_and_never_call_the_api()
    {
        var handler = RespondWith(StubHttpMessageHandler.Ok(Catalogue));
        var (client, _, error) = Build(handler);

        var exitCode = await client.RunAsync(["quote", "200", "300"]);

        Assert.Equal(1, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Contains(CommandLine.Usage, error.ToString(), StringComparison.Ordinal);
    }

    private static QuoteResponse Quote(string packageType, decimal cost)
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            packageType,
            cost,
            new DimensionsResponse(200, 300, 150, 9_000_000L),
            5m);

    /// <summary>Answers each request with the next canned response, in order.</summary>
    private static StubHttpMessageHandler RespondWith(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);

        return new StubHttpMessageHandler(_ => queue.Dequeue());
    }

    private static (ShippingConsoleClient Client, StringWriter Output, StringWriter Error) Build(
        StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080") };
        var output = new StringWriter();
        var error = new StringWriter();

        return (new ShippingConsoleClient(http, output, error), output, error);
    }
}
