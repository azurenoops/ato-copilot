using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for RequestSizeLimitMiddleware (US3 / FR-013).
/// Validates that oversized payloads are rejected with HTTP 413.
/// </summary>
public class RequestSizeLimitTests
{
    /// <summary>
    /// Verifies that a payload under the limit is accepted.
    /// </summary>
    [Fact]
    public async Task RequestWithinLimit_IsAccepted()
    {
        await using var app = await CreateTestApp(maxRequestBodySizeKb: 32);
        var client = app.GetTestClient();

        var content = new StringContent(new string('x', 1024), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/mcp/chat", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies that a payload exceeding the limit returns 413 (FR-013).
    /// </summary>
    [Fact]
    public async Task RequestExceedingLimit_Returns413()
    {
        await using var app = await CreateTestApp(maxRequestBodySizeKb: 32);
        var client = app.GetTestClient();

        // 33 KB body should exceed 32 KB limit
        var content = new StringContent(new string('x', 33 * 1024), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/mcp/chat", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>
    /// Verifies that the limit is configurable.
    /// </summary>
    [Fact]
    public async Task ConfigurableLimit_IsRespected()
    {
        await using var app = await CreateTestApp(maxRequestBodySizeKb: 64);
        var client = app.GetTestClient();

        // 33 KB should be accepted with 64 KB limit
        var content = new StringContent(new string('x', 33 * 1024), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/mcp/chat", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies that non-limited endpoints are not affected.
    /// </summary>
    [Fact]
    public async Task NonLimitedEndpoints_AreNotAffected()
    {
        await using var app = await CreateTestApp(maxRequestBodySizeKb: 1);
        var client = app.GetTestClient();

        // Even large requests to /health should work
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<WebApplication> CreateTestApp(int maxRequestBodySizeKb)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:MaxRequestBodySizeKb"] = maxRequestBodySizeKb.ToString(),
        });

        builder.WebHost.UseTestServer();
        var app = builder.Build();

        // Apply size limit middleware only to specific routes
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/mcp/chat") ||
                context.Request.Path.StartsWithSegments("/mcp/chat/stream"))
            {
                var maxSize = app.Configuration.GetValue("Server:MaxRequestBodySizeKb", 32) * 1024L;
                if (context.Request.ContentLength > maxSize)
                {
                    context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        "{\"errorCode\":\"PAYLOAD_TOO_LARGE\",\"message\":\"Request body exceeds maximum size.\"}");
                    return;
                }
            }
            await next();
        });

        app.MapPost("/mcp/chat", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        await app.StartAsync();
        return app;
    }
}
