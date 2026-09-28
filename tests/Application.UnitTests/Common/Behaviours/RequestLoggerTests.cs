using AiGateway.Application.Common.Behaviours;
using AiGateway.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace AiGateway.Application.UnitTests.Common.Behaviours;

public record TestCommand : IRequest
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
}

public class RequestLoggerTests
{
    private Mock<ILogger<TestCommand>> _logger = null!;
    private Mock<IUser> _user = null!;
    private Mock<IIdentityService> _identityService = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger<TestCommand>>();
        _user = new Mock<IUser>();
        _identityService = new Mock<IIdentityService>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        _user.Setup(x => x.Id).Returns(Guid.NewGuid().ToString());

        var requestLogger = new LoggingBehaviour<TestCommand>(_logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(new TestCommand { Id = 1, Title = "title" }, new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        var requestLogger = new LoggingBehaviour<TestCommand>(_logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(new TestCommand { Id = 1, Title = "title" }, new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }
}
