namespace AiGateway.Domain.Enums;

public enum RoutingStrategy
{
    Priority = 1,          // Primary provider first, fallback to next on error/rate-limit
    WeightedRandom = 2,    // Distribute load based on key/provider weights
    LowestCost = 3,        // Choose provider with lowest token cost for the model
    LowestLatency = 4      // Choose provider with lowest historical response time
}
