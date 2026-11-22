using System.Net;
using System.Text.Json;
using Crm.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Crm.Api.Tests.Extensions;

/// <summary>
/// Verifies <see cref="Api.Extensions.ForwardedHeadersExtensions"/> CIDR parsing and HTTP pipeline behavior
/// for trusted reverse proxies versus untrusted public clients.
/// </summary>
public class ForwardedHeadersExtensionsTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.18.0.2")]
    [InlineData("192.168.1.25")]
    public async Task DefaultKnownNetworksShouldAcceptForwardedHeadersFromLoopbackAndPrivateProxies(string proxyIp)
    {
        var ct = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder().Build();

        await using var app = await CreateProbeAppAsync(configuration, IPAddress.Parse(proxyIp), ct);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("X-Forwarded-For", "203.0.113.42");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("remoteIp").GetString().ShouldBe("203.0.113.42");
        doc.RootElement.GetProperty("scheme").GetString().ShouldBe("https");
    }

    [Fact]
    public async Task DefaultKnownNetworksShouldIgnoreForwardedHeadersFromPublicClientIp()
    {
        var ct = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder().Build();

        await using var app = await CreateProbeAppAsync(configuration, IPAddress.Parse("198.51.100.10"), ct);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("X-Forwarded-For", "203.0.113.42");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("remoteIp").GetString().ShouldBe("198.51.100.10");
        doc.RootElement.GetProperty("scheme").GetString().ShouldBe("http");
    }

    [Fact]
    public async Task CustomKnownNetworksShouldReplaceDefaultTrustedNetworks()
    {
        var ct = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "198.51.100.0/24",
            })
            .Build();

        await using var trustedApp = await CreateProbeAppAsync(configuration, IPAddress.Parse("198.51.100.10"), ct);
        using var trustedClient = trustedApp.GetTestClient();
        using var trustedRequest = new HttpRequestMessage(HttpMethod.Get, "/probe");
        trustedRequest.Headers.Add("X-Forwarded-For", "203.0.113.7");
        trustedRequest.Headers.Add("X-Forwarded-Proto", "https");

        var trustedResponse = await trustedClient.SendAsync(trustedRequest, ct);
        using var trustedDoc = JsonDocument.Parse(await trustedResponse.Content.ReadAsStringAsync(ct));
        trustedDoc.RootElement.GetProperty("remoteIp").GetString().ShouldBe("203.0.113.7");
        trustedDoc.RootElement.GetProperty("scheme").GetString().ShouldBe("https");

        await using var untrustedDockerApp = await CreateProbeAppAsync(configuration, IPAddress.Parse("172.18.0.2"), ct);
        using var untrustedDockerClient = untrustedDockerApp.GetTestClient();
        using var untrustedDockerRequest = new HttpRequestMessage(HttpMethod.Get, "/probe");
        untrustedDockerRequest.Headers.Add("X-Forwarded-For", "203.0.113.7");
        untrustedDockerRequest.Headers.Add("X-Forwarded-Proto", "https");

        var untrustedDockerResponse = await untrustedDockerClient.SendAsync(untrustedDockerRequest, ct);
        using var untrustedDoc = JsonDocument.Parse(await untrustedDockerResponse.Content.ReadAsStringAsync(ct));
        untrustedDoc.RootElement.GetProperty("remoteIp").GetString().ShouldBe("172.18.0.2");
        untrustedDoc.RootElement.GetProperty("scheme").GetString().ShouldBe("http");
    }

    [Fact]
    public async Task EmptyKnownNetworksShouldDisableAllForwardedHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks"] = string.Empty,
            })
            .Build();

        await using var app = await CreateProbeAppAsync(configuration, IPAddress.Parse("127.0.0.1"), ct);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("X-Forwarded-For", "203.0.113.42");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("remoteIp").GetString().ShouldBe("127.0.0.1");
        doc.RootElement.GetProperty("scheme").GetString().ShouldBe("http");
    }

    [Theory]
    [InlineData("not-a-cidr")]
    [InlineData("10.0.0.1")]
    [InlineData("   ")]
    public void InvalidCidrEntryShouldThrowInvalidOperationException(string invalidCidr)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = invalidCidr,
            })
            .Build();

        Should.Throw<InvalidOperationException>(() =>
            Api.Extensions.ForwardedHeadersExtensions.ResolveKnownNetworks(configuration));
    }

    private static async Task<WebApplication> CreateProbeAppAsync(
        IConfiguration configuration,
        IPAddress remoteIp,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddCustomForwardedHeaders(configuration);

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = remoteIp;
            await next();
        });
        app.UseForwardedHeaders();
        app.MapGet("/probe", (HttpContext context) => Results.Json(new
        {
            remoteIp = context.Connection.RemoteIpAddress?.ToString(),
            scheme = context.Request.Scheme,
        }));

        await app.StartAsync(cancellationToken);
        return app;
    }
}
