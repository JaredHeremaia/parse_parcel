using Shipping.Api.Endpoints;
using Shipping.Api.Infrastructure;
using Shipping.Core;
using Shipping.Core.Model;
using Shipping.Core.Quoting;

var builder = WebApplication.CreateBuilder(args);

// Packaging rules (weight limit, rotation) come from configuration with sane defaults.
var packagingOptions = builder.Configuration.GetSection("Packaging").Get<PackagingOptions>()
    ?? new PackagingOptions();
packagingOptions.Validate();

builder.Services.AddSingleton(packagingOptions);
builder.Services.AddPackageStorage(builder.Configuration);
builder.Services.AddScoped<PackageCatalog>();
builder.Services.AddScoped<IPackagingCalculator, PackagingCalculator>();

// Failures are reported as RFC 7807 problem details, including unhandled ones.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

await app.InitialisePackageStorageAsync();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" })).WithName("Health");
app.MapPackageEndpoints();
app.MapQuoteEndpoints();

app.Run();

/// <summary>Exposed so the integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
