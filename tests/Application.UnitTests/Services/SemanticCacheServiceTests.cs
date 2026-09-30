using AiGateway.Application.Common.Models.AiProxy;
using AiGateway.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Application.UnitTests.Services;

[TestFixture]
public class SemanticCacheServiceTests
{
    private Mock<IDistributedCache> _mockDistributedCache = null!;
    private MemoryCache _memoryCache = null!;
    private Mock<ILogger<SemanticCacheService>> _mockLogger = null!;
    private SemanticCacheService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDistributedCache = new Mock<IDistributedCache>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<SemanticCacheService>>();
        _service = new SemanticCacheService(_mockDistributedCache.Object, _memoryCache, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
    }

    [Test]
    public void GenerateSemanticKey_WithSameNormalizedPrompt_ProducesIdenticalKey()
    {
        // Arrange
        var messages1 = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "Hello, Guard! How are you?" }
        };

        var messages2 = new List<ChatMessageDto>
        {
            new() { Role = "User", Content = "hello guard how are you" }
        };

        // Act
        var key1 = _service.GenerateSemanticKey("gpt-4o", messages1);
        var key2 = _service.GenerateSemanticKey("GPT-4O", messages2);

        // Assert
        key1.ShouldNotBeNullOrWhiteSpace();
        key1.ShouldBe(key2);
    }

    [Test]
    public async Task GetCachedResponseAsync_WhenCacheMiss_ReturnsNull()
    {
        // Act
        var result = await _service.GetCachedResponseAsync("semcache:nonexistent:hash", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task SetAndGetCachedResponseAsync_ReturnsCachedResponse()
    {
        // Arrange
        var key = "semcache:test-model:12345";
        var originalResponse = new ChatCompletionResponse
        {
            Id = "chatcmpl-cached-1",
            Model = "test-model",
            Choices = new List<ChatChoiceDto>
            {
                new() { Index = 0, Message = new ChatMessageDto { Role = "assistant", Content = "Hello traveller!" } }
            }
        };

        // Act
        await _service.SetCachedResponseAsync(key, originalResponse, TimeSpan.FromMinutes(10), CancellationToken.None);
        var retrieved = await _service.GetCachedResponseAsync(key, CancellationToken.None);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe("chatcmpl-cached-1");
        retrieved.Choices.First().Message.Content.ShouldBe("Hello traveller!");
    }
}
