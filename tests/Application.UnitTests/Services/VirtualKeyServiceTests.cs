using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Infrastructure.Services;
using AiGateway.Application.UnitTests.Routing;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Application.UnitTests.Services;

[TestFixture]
public class VirtualKeyServiceTests
{
    private Mock<IApplicationDbContext> _mockContext = null!;
    private VirtualKeyService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _service = new VirtualKeyService(_mockContext.Object);
    }

    [Test]
    public void HashKey_ShouldReturnDeterministicSha256HexHash()
    {
        // Arrange
        var rawKey = "gw-live-testkey12345";

        // Act
        var hash1 = VirtualKeyService.HashKey(rawKey);
        var hash2 = VirtualKeyService.HashKey(rawKey);

        // Assert
        hash1.ShouldNotBeNullOrWhiteSpace();
        hash1.ShouldBe(hash2);
        hash1.Length.ShouldBe(64); // SHA-256 hex string is 64 chars
    }

    [Test]
    public void GenerateNewKey_ShouldGenerateKeyWithCorrectPrefixAndMask()
    {
        // Act
        var result = _service.GenerateNewKey("gw-live-");

        // Assert
        result.RawKey.ShouldStartWith("gw-live-");
        result.KeyPrefix.ShouldBe("gw-live-");
        result.KeyMask.ShouldStartWith("gw-live-...");
        result.KeyHash.ShouldBe(VirtualKeyService.HashKey(result.RawKey));
    }

    [Test]
    public async Task ValidateKeyAsync_WhenKeyIsValid_ShouldReturnVirtualKey()
    {
        // Arrange
        var rawKey = "gw-live-validkey123";
        var keyHash = VirtualKeyService.HashKey(rawKey);
        var vk = new VirtualKey
        {
            Id = 1,
            Name = "Valid Dev Key",
            KeyHash = keyHash,
            IsActive = true,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            MaxBudgetUsd = 100.0m,
            CurrentUsageUsd = 10.0m
        };

        var virtualKeys = new List<VirtualKey> { vk }.ToMockDbSet();
        _mockContext.Setup(c => c.VirtualKeys).Returns(virtualKeys.Object);

        // Act
        var result = await _service.ValidateKeyAsync(rawKey, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Name.ShouldBe("Valid Dev Key");
    }

    [Test]
    public async Task ValidateKeyAsync_WhenKeyIsInactive_ShouldReturnNull()
    {
        // Arrange
        var rawKey = "gw-live-inactivekey";
        var keyHash = VirtualKeyService.HashKey(rawKey);
        var vk = new VirtualKey
        {
            Id = 2,
            Name = "Inactive Key",
            KeyHash = keyHash,
            IsActive = false
        };

        var virtualKeys = new List<VirtualKey> { vk }.ToMockDbSet();
        _mockContext.Setup(c => c.VirtualKeys).Returns(virtualKeys.Object);

        // Act
        var result = await _service.ValidateKeyAsync(rawKey, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ValidateKeyAsync_WhenKeyIsExpired_ShouldReturnNull()
    {
        // Arrange
        var rawKey = "gw-live-expiredkey";
        var keyHash = VirtualKeyService.HashKey(rawKey);
        var vk = new VirtualKey
        {
            Id = 3,
            Name = "Expired Key",
            KeyHash = keyHash,
            IsActive = true,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) // Expired 5 mins ago
        };

        var virtualKeys = new List<VirtualKey> { vk }.ToMockDbSet();
        _mockContext.Setup(c => c.VirtualKeys).Returns(virtualKeys.Object);

        // Act
        var result = await _service.ValidateKeyAsync(rawKey, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ValidateKeyAsync_WhenBudgetExceeded_ShouldReturnNull()
    {
        // Arrange
        var rawKey = "gw-live-budgetexceeded";
        var keyHash = VirtualKeyService.HashKey(rawKey);
        var vk = new VirtualKey
        {
            Id = 4,
            Name = "Over Budget Key",
            KeyHash = keyHash,
            IsActive = true,
            MaxBudgetUsd = 50.0m,
            CurrentUsageUsd = 50.01m // Exceeded budget
        };

        var virtualKeys = new List<VirtualKey> { vk }.ToMockDbSet();
        _mockContext.Setup(c => c.VirtualKeys).Returns(virtualKeys.Object);

        // Act
        var result = await _service.ValidateKeyAsync(rawKey, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ValidateKeyAsync_WhenRawKeyIsEmpty_ShouldReturnNull()
    {
        // Act
        var result1 = await _service.ValidateKeyAsync("", CancellationToken.None);
        var result2 = await _service.ValidateKeyAsync("   ", CancellationToken.None);

        // Assert
        result1.ShouldBeNull();
        result2.ShouldBeNull();
    }

    [Test]
    public void IsModelAllowed_WhenAllowedModelAliasesIsNull_ReturnsTrue()
    {
        var vk = new VirtualKey { AllowedModelAliases = null };
        vk.IsModelAllowed("gpt-4o").ShouldBeTrue();
        vk.IsModelAllowed("gemini-1.5-flash").ShouldBeTrue();
    }

    [Test]
    public void IsModelAllowed_WhenModelIsInAllowedList_ReturnsTrue()
    {
        var vk = new VirtualKey { AllowedModelAliases = "gpt-4o-mini, gemini-1.5-flash" };
        vk.IsModelAllowed("gpt-4o-mini").ShouldBeTrue();
        vk.IsModelAllowed("GEMINI-1.5-FLASH").ShouldBeTrue(); // Case insensitive
    }

    [Test]
    public void IsModelAllowed_WhenModelIsNotInAllowedList_ReturnsFalse()
    {
        var vk = new VirtualKey { AllowedModelAliases = "gpt-4o-mini, gemini-1.5-flash" };
        vk.IsModelAllowed("gpt-4o").ShouldBeFalse();
        vk.IsModelAllowed("claude-3-5-sonnet").ShouldBeFalse();
    }
}
