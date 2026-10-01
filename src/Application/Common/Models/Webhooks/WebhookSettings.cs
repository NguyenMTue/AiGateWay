namespace AiGateway.Application.Common.Models.Webhooks;

public class WebhookSettings
{
    public const string SectionName = "WebhookSettings";

    public bool Enabled { get; set; } = true;

    public string? DiscordWebhookUrl { get; set; }

    public string? SlackWebhookUrl { get; set; }

    public decimal BudgetThresholdPercent { get; set; } = 90.0m;

    public double ProviderErrorRateThresholdPercent { get; set; } = 10.0;
}
