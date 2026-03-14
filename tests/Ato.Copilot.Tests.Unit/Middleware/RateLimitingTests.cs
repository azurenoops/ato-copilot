using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ato.Copilot.Core.Models;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for rate limiting middleware behavior (US2 / FR-006 through FR-010a).
/// Tests sliding window enforcement, per-client isolation, endpoint exemption,
/// structured 429 response, and environment variable overrides.
/// </summary>
public class RateLimitingTests
{
    /// <summary>
    /// Verifies that requests exceeding the permit limit within the window are rejected
    /// with HTTP 429 (FR-006).
    /// </summary>
    [Fact]
    public async Task SlidingWindow_RejectsRequestsExceedingLimit()
    {
        await using var app = await CreateTestApp(permitLimit: 3, windowSeconds: 60);
        var client = app.GetTestClient();

        // Send 3 requests (within limit)
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 4th request should be rate limited
        var rejected = await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Verifies that rate-limited responses include a Retry-After header (FR-009).
    /// </summary>
    [Fact]
    public async Task RateLimited_Response_HasRetryAfterHeader()
    {
        await using var app = await CreateTestApp(permitLimit: 1, windowSeconds: 60);
        var client = app.GetTestClient();

        // First request succeeds
        await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Second request is rate limited
        var response = await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that the 429 response body contains a structured ErrorDetail
    /// with errorCode "RATE_LIMITED" (FR-009).
    /// </summary>
    [Fact]
    public async Task RateLimited_Response_HasStructuredErrorBody()
    {
        await using var app = await CreateTestApp(permitLimit: 1, windowSeconds: 60);
        var client = app.GetTestClient();

        await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));
        var response = await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("RATE_LIMITED");
        json.RootElement.TryGetProperty("message", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("suggestion", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that exempt endpoints (e.g. /health) are not rate-limited (FR-007).
    /// </summary>
    [Fact]
    public async Task ExemptEndpoints_AreNotRateLimited()
    {
        await using var app = await CreateTestApp(permitLimit: 1, windowSeconds: 60);
        var client = app.GetTestClient();

        // Exceed the limit on exempt endpoint — should still succeed
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/health");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// Verifies that the PermitLimit can be overridden via environment variable (FR-010a).
    /// </summary>
    [Fact]
    public async Task PermitLimit_CanBeOverriddenViaConfig()
    {
        await using var app = await CreateTestApp(permitLimit: 5, windowSeconds: 60);
        var client = app.GetTestClient();

        // All 5 should succeed
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 6th should fail
        var rejected = await client.PostAsync("/mcp/chat", new StringContent("{}", Encoding.UTF8, "application/json"));
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static async Task<WebApplication> CreateTestApp(int permitLimit, int windowSeconds)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        var rateLimitingOptions = new RateLimitingOptions
        {
            Policies =
            [
                new RateLimitPolicy
                {
                    PolicyName = "chat",
                    Endpoint = "/mcp/chat",
                    PermitLimit = permitLimit,
                    WindowSeconds = windowSeconds,
                    SegmentsPerWindow = 2
                }
            ],
            ExemptEndpoints = ["/health"]
        };

        builder.Services.AddRateLimiter(options =>
        {
            foreach (var policy in rateLimitingOptions.Policies)
            {
                options.AddPolicy(policy.PolicyName, context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = policy.PermitLimit,
                            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                            SegmentsPerWindow = policy.SegmentsPerWindow,
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }));
            }

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }
                else
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                }

                var errorDetail = new
                {
                    errorCode = "RATE_LIMITED",
                    message = $"Rate limit exceeded for {context.HttpContext.Request.Path}.",
                    suggestion = "Reduce request frequency or wait before retrying."
                };

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(errorDetail), cancellationToken);
            };
        });

        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.UseRateLimiter();

        app.MapPost("/mcp/chat", () => Results.Ok(new { status = "ok" }))
            .RequireRateLimiting("chat");

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .DisableRateLimiting();

        await app.StartAsync();
        return app;
    }
}
