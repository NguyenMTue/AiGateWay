namespace AiGateway.Application.Common.Interfaces;

public interface ICircuitBreakerService
{
    /// <summary>
    /// Checks if the circuit for a specific Provider API Key is open (broken).
    /// </summary>
    bool IsCircuitOpen(int providerApiKeyId);

    /// <summary>
    /// Records a successful request, resetting failure counts and closing the circuit.
    /// </summary>
    void RecordSuccess(int providerApiKeyId);

    /// <summary>
    /// Records a failed request (e.g. 5xx, 429, Timeout). Trips the circuit to OPEN if threshold is reached.
    /// </summary>
    void RecordFailure(int providerApiKeyId, int httpStatusCode);

    /// <summary>
    /// Gets the remaining break duration in seconds if circuit is open.
    /// </summary>
    int GetRemainingBreakSeconds(int providerApiKeyId);
}
