using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Common.Models.Webhooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiGateway.Infrastructure.Services;

public class WebhookNotificationService : IWebhookNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebhookSettings _settings;
    private readonly ILogger<WebhookNotificationService> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAlertTimes = new();

    public TimeSpan AlertCooldown { get; set; } = TimeSpan.FromMinutes(15);

    public WebhookNotificationService(
        IHttpClientFactory httpClientFactory,
        IOptions<WebhookSettings> settings,
        ILogger<WebhookNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendVirtualKeyBudgetAlertAsync(
        int virtualKeyId,
        string keyName,
        string keyMask,
        decimal currentUsageUsd,
        decimal maxBudgetUsd,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        var deduplicationKey = $"Budget:{virtualKeyId}";
        if (IsAlertThrottled(deduplicationKey))
        {
            _logger.LogDebug("Budget alert for VirtualKey '{KeyName}' ({KeyId}) throttled due to cooldown.", keyName, virtualKeyId);
            return;
        }

        var percent = maxBudgetUsd > 0 ? (currentUsageUsd / maxBudgetUsd) * 100m : 0m;
        var title = "⚠️ Virtual Key Budget Warning (>= 90%)";
        var description = $"Virtual Key **{keyName}** (`{keyMask}`) has reached **{percent:F1}%** of its total budget!";

        var fields = new List<(string Name, string Value, bool Inline)>
        {
            ("Virtual Key", keyName, true),
            ("Key Mask", keyMask, true),
            ("Current Usage", $"${currentUsageUsd:F4} USD", true),
            ("Max Budget", $"${maxBudgetUsd:F2} USD", true),
            ("Usage Percent", $"{percent:F1}%", true),
            ("Status", "Nearing Limit", true)
        };

        var sentSuccess = await SendAlertAsync(title, description, fields, isWarning: true, cancellationToken);
        if (sentSuccess)
        {
            _lastAlertTimes[deduplicationKey] = DateTimeOffset.UtcNow;
            _logger.LogWarning("Webhook budget alert sent for VirtualKey '{KeyName}' ({KeyId}): {Usage}/${Budget} USD ({Percent:F1}%).",
                keyName, virtualKeyId, currentUsageUsd, maxBudgetUsd, percent);
        }
    }

    public async Task SendProviderHealthAlertAsync(
        int providerId,
        string providerName,
        double errorRatePercent,
        int totalRequests,
        int failedRequests,
        int error429Count,
        int error5xxCount,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        var deduplicationKey = $"Provider:{providerId}";
        if (IsAlertThrottled(deduplicationKey))
        {
            _logger.LogDebug("Provider health alert for '{ProviderName}' ({ProviderId}) throttled due to cooldown.", providerName, providerId);
            return;
        }

        var title = "🚨 High AI Provider Error Rate Alert (> 10%)";
        var description = $"AI Provider **{providerName}** has experienced a **{errorRatePercent:F1}%** error rate in recent requests!";

        var fields = new List<(string Name, string Value, bool Inline)>
        {
            ("Provider Name", providerName, true),
            ("Error Rate", $"{errorRatePercent:F1}%", true),
            ("Total Requests", totalRequests.ToString(), true),
            ("Failed Requests", failedRequests.ToString(), true),
            ("429 Rate Limits", error429Count.ToString(), true),
            ("5xx Server Errors", error5xxCount.ToString(), true)
        };

        var sentSuccess = await SendAlertAsync(title, description, fields, isWarning: false, cancellationToken);
        if (sentSuccess)
        {
            _lastAlertTimes[deduplicationKey] = DateTimeOffset.UtcNow;
            _logger.LogError("Webhook health alert sent for Provider '{ProviderName}' ({ProviderId}): Error Rate {ErrorRate:F1}%.",
                providerName, providerId, errorRatePercent);
        }
    }

    public async Task SendCircuitBreakerAlertAsync(
        int providerApiKeyId,
        string providerName,
        int consecutiveFailures,
        TimeSpan breakDuration,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled) return;

        var deduplicationKey = $"Circuit:{providerApiKeyId}";
        if (IsAlertThrottled(deduplicationKey))
        {
            _logger.LogDebug("Circuit breaker alert for ProviderApiKey {ApiKeyId} throttled due to cooldown.", providerApiKeyId);
            return;
        }

        var title = "🔴 Provider Circuit Breaker Tripped (OPEN)";
        var description = $"Circuit Breaker for AI Provider **{providerName}** (Key #{providerApiKeyId}) tripped to **OPEN** state after **{consecutiveFailures}** consecutive failures!";

        var fields = new List<(string Name, string Value, bool Inline)>
        {
            ("Provider Name", providerName, true),
            ("API Key ID", providerApiKeyId.ToString(), true),
            ("Consecutive Failures", consecutiveFailures.ToString(), true),
            ("Break Duration", $"{breakDuration.TotalSeconds} seconds", true),
            ("Action Taken", "Bypassing Provider for Fallback Model", false)
        };

        var sentSuccess = await SendAlertAsync(title, description, fields, isWarning: false, cancellationToken);
        if (sentSuccess)
        {
            _lastAlertTimes[deduplicationKey] = DateTimeOffset.UtcNow;
            _logger.LogError("Webhook Circuit Breaker alert sent for Provider '{ProviderName}' Key #{ApiKeyId}.", providerName, providerApiKeyId);
        }
    }

    private bool IsAlertThrottled(string key)
    {
        if (_lastAlertTimes.TryGetValue(key, out var lastSent))
        {
            if (DateTimeOffset.UtcNow - lastSent < AlertCooldown)
            {
                return true;
            }
        }
        return false;
    }

    private async Task<bool> SendAlertAsync(
        string title,
        string description,
        List<(string Name, string Value, bool Inline)> fields,
        bool isWarning,
        CancellationToken cancellationToken)
    {
        var hasDiscord = !string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrl);
        var hasSlack = !string.IsNullOrWhiteSpace(_settings.SlackWebhookUrl);

        if (!hasDiscord && !hasSlack)
        {
            _logger.LogDebug("Webhook notification skipped: No Discord or Slack Webhook URL configured.");
            return false;
        }

        var success = false;
        var client = _httpClientFactory.CreateClient("WebhookNotifier");

        if (hasDiscord)
        {
            var discordSuccess = await SendDiscordPayloadAsync(client, _settings.DiscordWebhookUrl!, title, description, fields, isWarning, cancellationToken);
            if (discordSuccess) success = true;
        }

        if (hasSlack)
        {
            var slackSuccess = await SendSlackPayloadAsync(client, _settings.SlackWebhookUrl!, title, description, fields, isWarning, cancellationToken);
            if (slackSuccess) success = true;
        }

        return success;
    }

    private async Task<bool> SendDiscordPayloadAsync(
        HttpClient client,
        string webhookUrl,
        string title,
        string description,
        List<(string Name, string Value, bool Inline)> fields,
        bool isWarning,
        CancellationToken cancellationToken)
    {
        try
        {
            var colorDecimal = isWarning ? 16753920 : 15158332; // Orange (#FFA500) or Red (#E74C3C)
            var discordFields = fields.Select(f => new
            {
                name = f.Name,
                value = f.Value,
                inline = f.Inline
            }).ToArray();

            var payload = new
            {
                username = "AI Gateway Alert Bot",
                avatar_url = "https://cdn-icons-png.flaticon.com/512/3662/3662817.png",
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = colorDecimal,
                        fields = discordFields,
                        footer = new { text = "AI Gateway Operations Telemetry" },
                        timestamp = DateTimeOffset.UtcNow.ToString("o")
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(webhookUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Discord Webhook failed with HTTP {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver Discord Webhook notification.");
            return false;
        }
    }

    private async Task<bool> SendSlackPayloadAsync(
        HttpClient client,
        string webhookUrl,
        string title,
        string description,
        List<(string Name, string Value, bool Inline)> fields,
        bool isWarning,
        CancellationToken cancellationToken)
    {
        try
        {
            var colorHex = isWarning ? "#FFA500" : "#E74C3C";
            var slackFields = fields.Select(f => new
            {
                title = f.Name,
                value = f.Value,
                @short = f.Inline
            }).ToArray();

            var payload = new
            {
                text = $"{title}\n{description}",
                attachments = new[]
                {
                    new
                    {
                        color = colorHex,
                        title,
                        text = description,
                        fields = slackFields,
                        ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(webhookUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Slack Webhook failed with HTTP {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver Slack Webhook notification.");
            return false;
        }
    }
}
