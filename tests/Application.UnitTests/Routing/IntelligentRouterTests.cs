using System.Linq.Expressions;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Domain.Entities;
using AiGateway.Domain.Enums;
using AiGateway.Infrastructure.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Application.UnitTests.Routing;

public static class TestDbSetExtensions
{
    public static Mock<DbSet<T>> ToMockDbSet<T>(this List<T> sourceList) where T : class
    {
        var queryable = sourceList.AsQueryable();
        var mockDbSet = new Mock<DbSet<T>>();

        mockDbSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

        mockDbSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

        mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        mockDbSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(sourceList.Add);

        return mockDbSet;
    }
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethods()
            .First(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
            .MakeGenericMethod(expectedResultType)
            .Invoke(_inner, new object[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }

    public TestAsyncEnumerable(Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }
}

[TestFixture]
public class IntelligentRouterTests
{
    private Mock<IApplicationDbContext> _mockContext = null!;
    private Mock<IEncryptionService> _mockEncryption = null!;
    private IntelligentRouter _router = null!;

    [SetUp]
    public void SetUp()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockEncryption = new Mock<IEncryptionService>();
        _mockEncryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => "decrypted-" + s);

        _router = new IntelligentRouter(_mockContext.Object, _mockEncryption.Object);
    }

    [Test]
    public async Task ResolveTargetAsync_WhenPrimaryModelFails_ShouldFallbackToSecondModel()
    {
        // Arrange
        var primaryKey = new ProviderApiKey { Id = 1, EncryptedApiKey = "key1", IsActive = true, Priority = 1 };
        var primaryModel = new AiModel
        {
            Id = 10,
            Name = "GPT-4o",
            ModelId = "gpt-4o",
            Alias = "gpt-4o",
            IsActive = true,
            Provider = new AiProvider { Id = 100, Name = "OpenAI", IsActive = true, ApiKeys = new List<ProviderApiKey> { primaryKey } }
        };

        var fallbackKey = new ProviderApiKey { Id = 2, EncryptedApiKey = "key2", IsActive = true, Priority = 1 };
        var fallbackModel = new AiModel
        {
            Id = 20,
            Name = "Gemini 1.5 Pro",
            ModelId = "gemini-1.5-pro",
            Alias = "gemini-1.5-pro",
            IsActive = true,
            Provider = new AiProvider { Id = 200, Name = "Google Gemini", IsActive = true, ApiKeys = new List<ProviderApiKey> { fallbackKey } }
        };

        var routeRule = new RouteRule
        {
            Id = 1,
            Name = "Smart Rule",
            TargetModelAlias = "smart-model",
            RoutingStrategy = RoutingStrategy.Priority,
            PrimaryModel = primaryModel,
            FallbackModel = fallbackModel,
            IsActive = true,
            Priority = 1
        };

        var routeRules = new List<RouteRule> { routeRule }.ToMockDbSet();
        var aiModels = new List<AiModel> { primaryModel, fallbackModel }.ToMockDbSet();

        _mockContext.Setup(c => c.RouteRules).Returns(routeRules.Object);
        _mockContext.Setup(c => c.AiModels).Returns(aiModels.Object);

        // Act 1: Initial call should resolve Primary Target (OpenAI)
        var primaryTarget = await _router.ResolveTargetAsync("smart-model", CancellationToken.None);
        primaryTarget.Model.Name.ShouldBe("GPT-4o");
        primaryTarget.ApiKey.Id.ShouldBe(1);

        // Act 2: Second call with primary key excluded (simulating primary provider failure)
        var fallbackTarget = await _router.ResolveTargetAsync("smart-model", CancellationToken.None, excludeApiKeyIds: new[] { 1 });

        // Assert: Should seamlessly failover to Gemini 1.5 Pro
        fallbackTarget.ShouldNotBeNull();
        fallbackTarget.Model.Name.ShouldBe("Gemini 1.5 Pro");
        fallbackTarget.ApiKey.Id.ShouldBe(2);
        fallbackTarget.DecryptedApiKey.ShouldBe("decrypted-key2");
    }

    [Test]
    public async Task HandleProviderFailureAsync_When429_ShouldSetCooldownUntil()
    {
        // Arrange
        var key = new ProviderApiKey { Id = 5, EncryptedApiKey = "key5", IsActive = true, CooldownUntil = null };
        var keys = new List<ProviderApiKey> { key }.ToMockDbSet();
        _mockContext.Setup(c => c.ProviderApiKeys).Returns(keys.Object);

        // Act
        await _router.HandleProviderFailureAsync(5, 429, CancellationToken.None);

        // Assert
        key.CooldownUntil.ShouldNotBeNull();
        key.CooldownUntil.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResolveTargetAsync_WithLowestCostStrategy_ShouldPickCheapestModel()
    {
        // Arrange: Primary model is expensive, Fallback model is cheaper
        var expensiveKey = new ProviderApiKey { Id = 1, EncryptedApiKey = "exp1", IsActive = true };
        var expensiveModel = new AiModel
        {
            Id = 10,
            Name = "GPT-4o",
            PromptTokenCostPer1K = 0.0025m,
            CompletionTokenCostPer1K = 0.0100m,
            IsActive = true,
            Provider = new AiProvider { Id = 100, Name = "OpenAI", IsActive = true, ApiKeys = new List<ProviderApiKey> { expensiveKey } }
        };

        var cheapKey = new ProviderApiKey { Id = 2, EncryptedApiKey = "cheap1", IsActive = true };
        var cheapModel = new AiModel
        {
            Id = 20,
            Name = "Gemini 1.5 Flash",
            PromptTokenCostPer1K = 0.000075m,
            CompletionTokenCostPer1K = 0.000300m,
            IsActive = true,
            Provider = new AiProvider { Id = 200, Name = "Google Gemini", IsActive = true, ApiKeys = new List<ProviderApiKey> { cheapKey } }
        };

        var routeRule = new RouteRule
        {
            Id = 2,
            Name = "Cost Rule",
            TargetModelAlias = "budget-model",
            RoutingStrategy = RoutingStrategy.LowestCost,
            PrimaryModel = expensiveModel,
            FallbackModel = cheapModel,
            IsActive = true
        };

        var routeRules = new List<RouteRule> { routeRule }.ToMockDbSet();
        var aiModels = new List<AiModel> { expensiveModel, cheapModel }.ToMockDbSet();

        _mockContext.Setup(c => c.RouteRules).Returns(routeRules.Object);
        _mockContext.Setup(c => c.AiModels).Returns(aiModels.Object);

        // Act: Should select the cheaper model (Gemini 1.5 Flash) first, even though GPT-4o is primary!
        var target = await _router.ResolveTargetAsync("budget-model", CancellationToken.None);

        // Assert
        target.ShouldNotBeNull();
        target.Model.Name.ShouldBe("Gemini 1.5 Flash");
        target.ApiKey.Id.ShouldBe(2);
    }
}
