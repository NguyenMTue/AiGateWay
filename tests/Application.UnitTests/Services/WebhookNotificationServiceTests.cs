using System.Net;
using System.Text.Json;
using AiGateway.Application.Common.Models.Webhooks;
using AiGateway.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Application.UnitTests.Services;

[TestFixture]
public class WebhookNotificationServiceTests
{
    private Mock<IHttpClientFactory> _mockHttpClientFactory = null!;
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private Mock<ILogger<WebhookNotificationService>> _mockLogger = null!;
    private WebhookSettings _webhookSettings = null!;

    [SetUp]
    public void SetUp()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}")
            });

        var client = new HttpClient(_mockHttpMessageHandler.Object);

        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(client);

        _mockLogger = new Mock<ILogger<WebhookNotificationService>>();

        _webhookSettings = new WebhookSettings
        {
            Enabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/test",
            SlackWebhookUrl = "https://hooks.slack.com/services/test",
            BudgetThresholdPercent = 90.0m,
            ProviderErrorRateThresholdPercent = 10.0
        };
    }

    [Test]
    public async Task SendVirtualKeyBudgetAlertAsync_SendsDiscordAndSlackPayloads()
    {
        // Arrange
        var options = Options.Create(_webhookSettings);
        var service = new WebhookNotificationService(_mockHttpClientFactory.Object, options, _mockLogger.Object);

        // Act
        await service.SendVirtualKeyBudgetAlertAsync(
            virtualKeyId: 10,
            keyName: "GameClientKey",
            keyMask: "gw-live-...ab12",
            currentUsageUsd: 95.50m,
            maxBudgetUsd: 100.00m);

        // Assert - Should make 2 HTTP calls (1 for Discord, 1 for Slack)
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task SendProviderHealthAlertAsync_SendsDiscordAndSlackPayloads()
    {
        // Arrange
        var options = Options.Create(_webhookSettings);
        var service = new WebhookNotificationService(_mockHttpClientFactory.Object, options, _mockLogger.Object);

        // Act
        await service.SendProviderHealthAlertAsync(
            providerId: 1,
            providerName: "OpenAI",
            errorRatePercent: 15.5,
            totalRequests: 100,
            failedRequests: 16,
            error429Count: 10,
            error5xxCount: 6);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task SendCircuitBreakerAlertAsync_SendsDiscordAndSlackPayloads()
    {
        // Arrange
        var options = Options.Create(_webhookSettings);
        var service = new WebhookNotificationService(_mockHttpClientFactory.Object, options, _mockLogger.Object);

        // Act
        await service.SendCircuitBreakerAlertAsync(
            providerApiKeyId: 5,
            providerName: "Gemini",
            consecutiveFailures: 3,
            breakDuration: TimeSpan.FromSeconds(60));

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task SendVirtualKeyBudgetAlertAsync_WhenThrottled_DoesNotResendWithinCooldown()
    {
        // Arrange
        var options = Options.Create(_webhookSettings);
        var service = new WebhookNotificationService(_mockHttpClientFactory.Object, options, _mockLogger.Object)
        {
            AlertCooldown = TimeSpan.FromMinutes(15)
        };

        // Act 1: First call -> sent
        await service.SendVirtualKeyBudgetAlertAsync(1, "Key1", "gw-live-...1111", 92m, 100m);

        // Act 2: Immediate second call -> throttled
        await service.SendVirtualKeyBudgetAlertAsync(1, "Key1", "gw-live-...1111", 95m, 100m);

        // Assert - Only 2 calls made from the first invocation (Discord + Slack)
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task SendAlert_WhenDisabled_DoesNotSendAnyHttpRequests()
    {
        // Arrange
        _webhookSettings.Enabled = false;
        var options = Options.Create(_webhookSettings);
        var service = new WebhookNotificationService(_mockHttpClientFactory.Object, options, _mockLogger.Object);

        // Act
        await service.SendVirtualKeyBudgetAlertAsync(1, "DisabledKey", "gw-live-...0000", 95m, 100m);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}
