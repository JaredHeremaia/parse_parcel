using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Shipping.Api.Tests;

/// <summary>
/// Hosts the real API in memory. Storage is pinned to the in-memory provider so the
/// suite needs no database, and each factory gets a fresh catalogue, which keeps
/// the write tests from leaking into each other.
/// </summary>
internal sealed class ShippingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "InMemory",
                ["Packaging:MaxWeightKg"] = "25",
                ["Packaging:AllowRotation"] = "true",
            }));
    }
}
