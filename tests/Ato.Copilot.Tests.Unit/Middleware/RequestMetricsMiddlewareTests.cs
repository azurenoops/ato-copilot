using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Ato.Copilot.Core.Observability;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for RequestMetricsMiddleware (US5 / FR-045).
/// Verifies that request metrics are recorded on completion.
/// </summary>
public class RequestMetricsMiddlewareTests
{
    [Fact]
    public async Task Middleware_RecordsMetrics_OnRequestCompletion()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.Services.AddSingleton<HttpMetrics>();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseMiddleware<Ato.Copilot.Mcp.Middleware.RequestMetricsMiddleware>();
        app.MapGet("/test", () => Results.Ok(new { status = "ok" }));

        await app.StartAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Middleware_RecordsMetrics_OnErrorResponse()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.Services.AddSingleton<HttpMetrics>();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseMiddleware<Ato.Copilot.Mcp.Middleware.RequestMetricsMiddleware>();
        app.MapGet("/error", () => Results.StatusCode(500));

        await app.StartAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/error");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        await app.StopAsync();
        await app.DisposeAsync();
    }
}
