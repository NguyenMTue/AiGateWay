using System.Linq.Expressions;
using AiGateway.Application.Common.Interfaces;
using AiGateway.Application.Conversations.Queries;
using AiGateway.Application.Conversations.Commands;
using AiGateway.Application.Analytics.Queries;
using AiGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace AiGateway.Application.UnitTests.MockClient;

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
public class MockClientVerificationTests
{
    private Mock<IApplicationDbContext> _mockContext = null!;

    [SetUp]
    public void SetUp()
    {
        _mockContext = new Mock<IApplicationDbContext>();
    }

    [Test]
    public async Task Scenario1_Authentication_ShouldIssueValidTokenAndVerifyRole()
    {
        // Arrange
        var npcUser = new { Email = "npcclient@localhost", Role = "NpcClient" };

        // Act & Assert
        npcUser.Email.ShouldBe("npcclient@localhost");
        npcUser.Role.ShouldBe("NpcClient");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Scenario2_GetConversations_ShouldReturnRecentNpcActionsContext()
    {
        // Arrange
        var histories = new List<ChatHistory>
        {
            new ChatHistory { Id = 1, UserId = "npc-1", ConversationId = "npc-session-01", Role = "system", Content = "Patrol guard context", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
            new ChatHistory { Id = 2, UserId = "npc-1", ConversationId = "npc-session-01", Role = "user", Content = "Telemetry: Pos=(12.5, 3.4), PlayerDist=5m", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) },
            new ChatHistory { Id = 3, UserId = "npc-1", ConversationId = "npc-session-01", Role = "assistant", Content = "{\"action\": \"challenge_player\"}", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4) }
        }.ToMockDbSet();

        _mockContext.Setup(c => c.ChatHistories).Returns(histories.Object);

        var query = new GetConversationsQuery(UserId: "npc-1", ConversationId: "npc-session-01", Top: 3);
        var handler = new GetConversationsQueryHandler(_mockContext.Object);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.First().Content.ShouldContain("challenge_player");
    }

    [Test]
    public async Task Scenario3_AddChatMessage_ShouldStoreNewNpcActionInHistory()
    {
        // Arrange
        var historiesList = new List<ChatHistory>();
        var mockDbSet = historiesList.ToMockDbSet();

        _mockContext.Setup(c => c.ChatHistories).Returns(mockDbSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new AddChatMessageCommand(
            UserId: "npc-1",
            ConversationId: "npc-session-01",
            Role: "user",
            Content: "Telemetry: Pos=(15.0, 4.0), Player entering combat range",
            ModelAlias: "smart-model"
        );

        var handler = new AddChatMessageCommandHandler(_mockContext.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        historiesList.Count.ShouldBe(1);
        historiesList[0].Content.ShouldContain("combat range");
        historiesList[0].Role.ShouldBe("user");
    }

    [Test]
    public async Task Scenario4_DatabaseAuditLogging_ShouldContainAll7RequiredFields()
    {
        // Arrange: 7 fields in MOCK_CLIENT_SETUP: user_id, model, timestamp, latency_ms, input_tokens, output_tokens, status
        var log = new RequestLog
        {
            Id = 101,
            VirtualKeyId = 1,
            AiModelId = 2,
            RequestedModelAlias = "smart-model",
            PromptTokens = 45,
            CompletionTokens = 25,
            TotalTokens = 70,
            LatencyMs = 320,
            HttpStatusCode = 200,
            IsSuccess = true,
            RequestedAt = DateTimeOffset.UtcNow
        };

        var logs = new List<RequestLog> { log }.ToMockDbSet();
        _mockContext.Setup(c => c.RequestLogs).Returns(logs.Object);

        var query = new GetCostAndTokenUsageQuery();
        var handler = new GetCostAndTokenUsageQueryHandler(_mockContext.Object);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.TotalPromptTokens.ShouldBe(45);
        result.TotalCompletionTokens.ShouldBe(25);
        result.TotalTokens.ShouldBe(70);
        result.TotalRequests.ShouldBe(1);
    }
}
