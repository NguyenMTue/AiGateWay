using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiGateway.Application.Analytics;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.VirtualKeys;
using AiGateway.Application.VirtualKeys.Commands;
using AiGateway.Domain.Entities;
using AiGateway.Application.UnitTests.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Infrastructure.IntegrationTests;

[TestFixture]
public class EndpointsIntegrationTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IApplicationDbContext> _mockContext = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _mockContext = new Mock<IApplicationDbContext>();

        var provider = new AiProvider { Id = 1, Name = "OpenAI", Slug = "openai", IsActive = true };
        var model = new AiModel { Id = 1, Name = "GPT-4o", ModelId = "gpt-4o", Alias = "gpt-4o", Provider = provider, IsActive = true };
        var virtualKey = new VirtualKey { Id = 1, Name = "Dev Key", KeyHash = "hash123", IsActive = true };

        var modelsList = new List<AiModel> { model }.ToMockDbSet();
        var virtualKeysList = new List<VirtualKey> { virtualKey }.ToMockDbSet();
        var requestLogsList = new List<RequestLog>().ToMockDbSet();
        var routeRulesList = new List<RouteRule>().ToMockDbSet();

        _mockContext.Setup(c => c.AiModels).Returns(modelsList.Object);
        _mockContext.Setup(c => c.VirtualKeys).Returns(virtualKeysList.Object);
        _mockContext.Setup(c => c.RequestLogs).Returns(requestLogsList.Object);
        _mockContext.Setup(c => c.RouteRules).Returns(routeRulesList.Object);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Environment", "Testing");
                builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
                builder.UseSetting("ConnectionStrings:AiGatewayDb", "Host=localhost;Database=test;Username=test;Password=test");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IApplicationDbContext>();
                    services.AddScoped(_ => _mockContext.Object);
                });
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetModels_ShouldReturnOkAndActiveModelList()
    {
        // Act
        var response = await _client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.TryGetProperty("object", out var objectProp).ShouldBeTrue();
        objectProp.GetString().ShouldBe("list");
        content.TryGetProperty("data", out var dataProp).ShouldBeTrue();
        dataProp.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Test]
    public async Task GetAnalyticsCostUsage_ShouldReturnOkAndReport()
    {
        // Act
        var response = await _client.GetAsync("/api/analytics/cost-usage");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<CostAndTokenUsageReportDto>();
        report.ShouldNotBeNull();
        report.TotalRequests.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task CreateVirtualKey_ShouldCreateNewKeyAndReturnRawKey()
    {
        // Arrange
        var command = new CreateVirtualKeyCommand(
            Name: "Integration Test Key",
            RateLimitRpm: 120,
            RateLimitTpm: 50000,
            MaxBudgetUsd: 25.0m,
            ExpiresAt: null,
            KeyPrefix: "gw-test-"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/virtual-keys/create", command);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<CreatedVirtualKeyResponseDto>();
        created.ShouldNotBeNull();
        created.RawVirtualKey.ShouldStartWith("gw-test-");
        created.Name.ShouldBe("Integration Test Key");
        created.KeyMask.ShouldStartWith("gw-test-...");
    }

    [Test]
    public async Task ChatCompletions_WithoutVirtualKey_ShouldReturnUnauthorized()
    {
        // Arrange
        var payload = new
        {
            model = "gpt-4o",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
