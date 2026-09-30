using AiGateway.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;
using System.Text;

namespace AiGateway.Application.UnitTests.Services;

[TestFixture]
public class RateLimitServiceTests
{
    private Mock<IDistributedCache> _mockDistributedCache = null!;
    private MemoryCache _memoryCache = null!;
    private Mock<ILogger<RateLimitService>> _mockLogger = null!;
    private RateLimitService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDistributedCache = new Mock<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<RateLimitService>>();
        _service = new RateLimitService(_mockDistributedCache.Object, _memoryCache, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
    }

    [Test]
    public async Task CheckAndRecordAsync_WhenWithinLimits_AllowsAndIncrements()
    {
        // Arrange
        var vkId = 1;
        var limitRpm = 10;
        var limitTpm = 1000;
        var estimatedTokens = 100;

        _mockDistributedCache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!);

        // Act
        var result = await _service.CheckAndRecordAsync(vkId, limitRpm, limitTpm, estimatedTokens, CancellationToken.None);

        // Assert
        result.IsAllowed.ShouldBeTrue();
        result.Reason.ShouldBeNull();
        result.CurrentRpm.ShouldBe(1);
        result.CurrentTpm.ShouldBe(100);
    }

    [Test]
    public async Task CheckAndRecordAsync_WhenRpmExceeded_RejectsRequest()
    {
        // Arrange
        var vkId = 2;
        var limitRpm = 5;
        var limitTpm = 10000;

        // Mock distributed cache returning RPM = 5
        _mockDistributedCache
            .Setup(c => c.GetAsync(It.Is<string>(k => k.Contains("rl:rpm")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("5"));

        _mockDistributedCache
            .Setup(c => c.GetAsync(It.Is<string>(k => k.Contains("rl:tpm")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("100"));

        // Act
        var result = await _service.CheckAndRecordAsync(vkId, limitRpm, limitTpm, 50, CancellationToken.None);

        // Assert
        result.IsAllowed.ShouldBeFalse();
        result.Reason.ShouldNotBeNull();
        result.Reason!.ShouldContain("RPM limit");
        result.CurrentRpm.ShouldBe(5);
    }

    [Test]
    public async Task CheckAndRecordAsync_WhenTpmExceeded_RejectsRequest()
    {
        // Arrange
        var vkId = 3;
        var limitRpm = 100;
        var limitTpm = 500;

        _mockDistributedCache
            .Setup(c => c.GetAsync(It.Is<string>(k => k.Contains("rl:rpm")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("1"));

        _mockDistributedCache
            .Setup(c => c.GetAsync(It.Is<string>(k => k.Contains("rl:tpm")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("450"));

        // Act (requesting 100 tokens: 450 + 100 = 550 > 500 limit)
        var result = await _service.CheckAndRecordAsync(vkId, limitRpm, limitTpm, 100, CancellationToken.None);

        // Assert
        result.IsAllowed.ShouldBeFalse();
        result.Reason.ShouldNotBeNull();
        result.Reason!.ShouldContain("TPM limit");
        result.CurrentTpm.ShouldBe(450);
    }

    [Test]
    public async Task CheckAndRecordAsync_WhenDistributedCacheThrows_FallsBackToMemoryCache()
    {
        // Arrange
        var vkId = 4;
        var limitRpm = 10;
        var limitTpm = 1000;

        // Mock distributed cache throwing exception
        _mockDistributedCache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        _mockDistributedCache
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        // Act - Call twice, fallback to memory cache should track local count
        var result1 = await _service.CheckAndRecordAsync(vkId, limitRpm, limitTpm, 50, CancellationToken.None);
        var result2 = await _service.CheckAndRecordAsync(vkId, limitRpm, limitTpm, 50, CancellationToken.None);

        // Assert
        result1.IsAllowed.ShouldBeTrue();
        result1.CurrentRpm.ShouldBe(1);

        result2.IsAllowed.ShouldBeTrue();
        result2.CurrentRpm.ShouldBe(2);
        result2.CurrentTpm.ShouldBe(100);
    }
}
