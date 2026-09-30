using System.Collections.Concurrent;
using AiGateway.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiGateway.Infrastructure.Services;

public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly ConcurrentDictionary<int, CircuitStateData> _circuits = new();

    private const int DefaultFailureThreshold = 3;
    private static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromSeconds(60);

    public CircuitBreakerService(ILogger<CircuitBreakerService> logger)
    {
        _logger = logger;
    }

    public bool IsCircuitOpen(int providerApiKeyId)
    {
        if (!_circuits.TryGetValue(providerApiKeyId, out var state))
        {
            return false;
        }

        lock (state)
        {
            if (state.State == CircuitState.Open)
            {
                if (DateTimeOffset.UtcNow >= state.OpenUntil)
                {
                    // Transition to HalfOpen (allow a trial request)
                    state.State = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit Breaker for ProviderApiKey {ApiKeyId} transitioned to HALF-OPEN (testing recovery).", providerApiKeyId);
                    return false;
                }

                return true; // Circuit is still open
            }

            return false;
        }
    }

    public void RecordSuccess(int providerApiKeyId)
    {
        if (!_circuits.TryGetValue(providerApiKeyId, out var state))
        {
            return;
        }

        lock (state)
        {
            if (state.State != CircuitState.Closed || state.ConsecutiveFailures > 0)
            {
                _logger.LogInformation("Circuit Breaker for ProviderApiKey {ApiKeyId} RESET to CLOSED after successful response.", providerApiKeyId);
            }

            state.ConsecutiveFailures = 0;
            state.State = CircuitState.Closed;
            state.OpenUntil = null;
        }
    }

    public void RecordFailure(int providerApiKeyId, int httpStatusCode)
    {
        // Only 429 Too Many Requests and 5xx Server Errors trip the circuit
        if (httpStatusCode != 429 && httpStatusCode < 500)
        {
            return;
        }

        var state = _circuits.GetOrAdd(providerApiKeyId, _ => new CircuitStateData());

        lock (state)
        {
            state.ConsecutiveFailures++;
            _logger.LogWarning("ProviderApiKey {ApiKeyId} reported HTTP {StatusCode} error. Consecutive failures: {Failures}/{Threshold}.",
                providerApiKeyId, httpStatusCode, state.ConsecutiveFailures, DefaultFailureThreshold);

            if (state.ConsecutiveFailures >= DefaultFailureThreshold && state.State != CircuitState.Open)
            {
                state.State = CircuitState.Open;
                state.OpenUntil = DateTimeOffset.UtcNow.Add(DefaultBreakDuration);
                _logger.LogError("Circuit Breaker for ProviderApiKey {ApiKeyId} TRIPPED to OPEN until {OpenUntil} due to {Failures} consecutive failures.",
                    providerApiKeyId, state.OpenUntil, state.ConsecutiveFailures);
            }
        }
    }

    public int GetRemainingBreakSeconds(int providerApiKeyId)
    {
        if (!_circuits.TryGetValue(providerApiKeyId, out var state))
        {
            return 0;
        }

        lock (state)
        {
            if (state.State == CircuitState.Open && state.OpenUntil.HasValue)
            {
                var remaining = (int)(state.OpenUntil.Value - DateTimeOffset.UtcNow).TotalSeconds;
                return Math.Max(0, remaining);
            }

            return 0;
        }
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    private class CircuitStateData
    {
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenUntil { get; set; }
    }
}
