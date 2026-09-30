using AiGateway.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Application.UnitTests.Services;

[TestFixture]
public class CircuitBreakerServiceTests
{
    private Mock<ILogger<CircuitBreakerService>> _mockLogger = null!;
    private CircuitBreakerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<CircuitBreakerService>>();
        _service = new CircuitBreakerService(_mockLogger.Object);
    }

    [Test]
    public void IsCircuitOpen_Initially_ReturnsFalse()
    {
        // Act
        var isOpen = _service.IsCircuitOpen(1);

        // Assert
        isOpen.ShouldBeFalse();
    }

    [Test]
    public void RecordFailure_WhenUnderThreshold_CircuitStaysClosed()
    {
        // Act (2 failures, threshold is 3)
        _service.RecordFailure(1, 500);
        _service.RecordFailure(1, 503);

        // Assert
        _service.IsCircuitOpen(1).ShouldBeFalse();
    }

    [Test]
    public void RecordFailure_WhenThresholdReached_TripsCircuitToOpen()
    {
        // Act (3 consecutive 5xx errors)
        _service.RecordFailure(1, 500);
        _service.RecordFailure(1, 502);
        _service.RecordFailure(1, 429);

        // Assert
        _service.IsCircuitOpen(1).ShouldBeTrue();
        _service.GetRemainingBreakSeconds(1).ShouldBeGreaterThan(0);
    }

    [Test]
    public void RecordFailure_When4xxNonRateLimitError_DoesNotTripCircuit()
    {
        // Act (400 Bad Request or 404 Not Found shouldn't trip provider circuit)
        _service.RecordFailure(1, 400);
        _service.RecordFailure(1, 404);
        _service.RecordFailure(1, 400);

        // Assert
        _service.IsCircuitOpen(1).ShouldBeFalse();
    }

    [Test]
    public void RecordSuccess_WhenCircuitIsOpen_ResetsCircuitToClosed()
    {
        // Arrange (Trip circuit)
        _service.RecordFailure(1, 500);
        _service.RecordFailure(1, 500);
        _service.RecordFailure(1, 500);
        _service.IsCircuitOpen(1).ShouldBeTrue();

        // Act
        _service.RecordSuccess(1);

        // Assert
        _service.IsCircuitOpen(1).ShouldBeFalse();
        _service.GetRemainingBreakSeconds(1).ShouldBe(0);
    }
}
