using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MemberPropertyAlert.Core.Application.Commands;
using MemberPropertyAlert.Core.Application.Queries;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Functions.Services;
using Moq;
using Xunit;

namespace MemberPropertyAlert.Functions.Tests.Services;

public class InstitutionHandlersTests
{
    private readonly IFixture _fixture;
    private readonly Mock<ILogger<CreateInstitutionCommandHandler>> _createLogger;
    private readonly Mock<ILogger<UpdateInstitutionCommandHandler>> _updateLogger;
    private readonly Mock<ILogger<DeleteInstitutionCommandHandler>> _deleteLogger;
    private readonly Mock<ILogger<GetAllInstitutionsQueryHandler>> _getAllLogger;
    private readonly Mock<ILogger<GetInstitutionByIdQueryHandler>> _getByIdLogger;
    private readonly Mock<ICosmosService> _cosmosService;

    public InstitutionHandlersTests()
    {
        _fixture = new Fixture().Customize(new AutoMoqCustomization());
        _createLogger = new Mock<ILogger<CreateInstitutionCommandHandler>>();
        _updateLogger = new Mock<ILogger<UpdateInstitutionCommandHandler>>();
        _deleteLogger = new Mock<ILogger<DeleteInstitutionCommandHandler>>();
        _getAllLogger = new Mock<ILogger<GetAllInstitutionsQueryHandler>>();
        _getByIdLogger = new Mock<ILogger<GetInstitutionByIdQueryHandler>>();
        _cosmosService = new Mock<ICosmosService>();
    }

    [Fact]
    public async Task CreateInstitutionCommandHandler_WithValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = new CreateInstitutionCommand
        {
            Name = "Test Institution",
            ContactEmail = "test@example.com",
            WebhookUrl = "https://example.com/webhook",
            IsActive = true
        };
        
        // Setup the mock to return the exact institution that was passed to it
        _cosmosService.Setup(x => x.CreateInstitutionAsync(It.IsAny<Institution>()))
            .ReturnsAsync((Institution institution) => 
            {
                // Return the same institution object that was passed in
                return institution;
            });

        var handler = new CreateInstitutionCommandHandler(_createLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(command.Name);
        result.Value.ContactEmail.Should().Be(command.ContactEmail);
        result.Value.WebhookUrl.Should().Be(command.WebhookUrl);
        result.Value.IsActive.Should().Be(command.IsActive);
        result.Value.Id.Should().NotBeNullOrEmpty();
        result.Value.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        _cosmosService.Verify(x => x.CreateInstitutionAsync(It.Is<Institution>(i => 
            i.Name == command.Name && 
            i.ContactEmail == command.ContactEmail && 
            i.WebhookUrl == command.WebhookUrl && 
            i.IsActive == command.IsActive)), Times.Once);
    }

    [Fact]
    public async Task CreateInstitutionCommandHandler_WhenCosmosServiceThrows_ShouldReturnFailure()
    {
        // Arrange
        var command = _fixture.Create<CreateInstitutionCommand>();
        var expectedException = new Exception("Database error");
        
        _cosmosService.Setup(x => x.CreateInstitutionAsync(It.IsAny<Institution>()))
            .ThrowsAsync(expectedException);

        var handler = new CreateInstitutionCommandHandler(_createLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to create institution");
        result.Error.Should().Contain("Database error");
    }

    [Fact]
    public async Task UpdateInstitutionCommandHandler_WithValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = _fixture.Create<UpdateInstitutionCommand>();
        var existingInstitution = _fixture.Create<Institution>();
        var updatedInstitution = _fixture.Create<Institution>();
        
        _cosmosService.Setup(x => x.GetInstitutionAsync(command.Id))
            .ReturnsAsync(existingInstitution);
        _cosmosService.Setup(x => x.UpdateInstitutionAsync(It.IsAny<Institution>()))
            .ReturnsAsync(updatedInstitution);

        var handler = new UpdateInstitutionCommandHandler(_updateLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        
        _cosmosService.Verify(x => x.GetInstitutionAsync(command.Id), Times.Once);
        _cosmosService.Verify(x => x.UpdateInstitutionAsync(It.IsAny<Institution>()), Times.Once);
    }

    [Fact]
    public async Task UpdateInstitutionCommandHandler_WhenInstitutionNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = _fixture.Create<UpdateInstitutionCommand>();
        
        _cosmosService.Setup(x => x.GetInstitutionAsync(command.Id))
            .ReturnsAsync((Institution?)null);

        var handler = new UpdateInstitutionCommandHandler(_updateLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain($"Institution with ID {command.Id} not found");
        
        _cosmosService.Verify(x => x.GetInstitutionAsync(command.Id), Times.Once);
        _cosmosService.Verify(x => x.UpdateInstitutionAsync(It.IsAny<Institution>()), Times.Never);
    }

    [Fact]
    public async Task DeleteInstitutionCommandHandler_WithValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = _fixture.Create<DeleteInstitutionCommand>();
        var existingInstitution = _fixture.Create<Institution>();
        
        _cosmosService.Setup(x => x.GetInstitutionAsync(command.Id))
            .ReturnsAsync(existingInstitution);
        _cosmosService.Setup(x => x.DeleteInstitutionAsync(command.Id))
            .Returns(Task.CompletedTask);

        var handler = new DeleteInstitutionCommandHandler(_deleteLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        _cosmosService.Verify(x => x.GetInstitutionAsync(command.Id), Times.Once);
        _cosmosService.Verify(x => x.DeleteInstitutionAsync(command.Id), Times.Once);
    }

    [Fact]
    public async Task DeleteInstitutionCommandHandler_WhenInstitutionNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = _fixture.Create<DeleteInstitutionCommand>();
        
        _cosmosService.Setup(x => x.GetInstitutionAsync(command.Id))
            .ReturnsAsync((Institution?)null);

        var handler = new DeleteInstitutionCommandHandler(_deleteLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain($"Institution with ID {command.Id} not found");
        
        _cosmosService.Verify(x => x.GetInstitutionAsync(command.Id), Times.Once);
        _cosmosService.Verify(x => x.DeleteInstitutionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAllInstitutionsQueryHandler_ShouldReturnAllInstitutions()
    {
        // Arrange
        var query = new GetAllInstitutionsQuery { ActiveOnly = false };
        var institutions = _fixture.CreateMany<Institution>(3).ToList();
        
        _cosmosService.Setup(x => x.GetAllInstitutionsAsync())
            .ReturnsAsync(institutions);

        var handler = new GetAllInstitutionsQueryHandler(_getAllLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().BeEquivalentTo(institutions);
        
        _cosmosService.Verify(x => x.GetAllInstitutionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllInstitutionsQueryHandler_WithActiveOnly_ShouldReturnOnlyActiveInstitutions()
    {
        // Arrange
        var query = new GetAllInstitutionsQuery { ActiveOnly = true };
        var institutions = new List<Institution>
        {
            _fixture.Build<Institution>().With(x => x.IsActive, true).Create(),
            _fixture.Build<Institution>().With(x => x.IsActive, false).Create(),
            _fixture.Build<Institution>().With(x => x.IsActive, true).Create()
        };
        
        _cosmosService.Setup(x => x.GetAllInstitutionsAsync())
            .ReturnsAsync(institutions);

        var handler = new GetAllInstitutionsQueryHandler(_getAllLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(x => x.IsActive);
        
        _cosmosService.Verify(x => x.GetAllInstitutionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetInstitutionByIdQueryHandler_WithValidId_ShouldReturnInstitution()
    {
        // Arrange
        var query = _fixture.Create<GetInstitutionByIdQuery>();
        var institution = _fixture.Create<Institution>();
        
        _cosmosService.Setup(x => x.GetInstitutionAsync(query.Id))
            .ReturnsAsync(institution);

        var handler = new GetInstitutionByIdQueryHandler(_getByIdLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(institution);
        
        _cosmosService.Verify(x => x.GetInstitutionAsync(query.Id), Times.Once);
    }

    [Fact]
    public async Task GetInstitutionByIdQueryHandler_WhenInstitutionNotFound_ShouldReturnFailure()
    {
        // Arrange
        var query = _fixture.Create<GetInstitutionByIdQuery>();
        
        _cosmosService.Setup(x => x.GetInstitutionAsync(query.Id))
            .ReturnsAsync((Institution?)null);

        var handler = new GetInstitutionByIdQueryHandler(_getByIdLogger.Object, _cosmosService.Object);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain($"Institution with ID {query.Id} not found");
        
        _cosmosService.Verify(x => x.GetInstitutionAsync(query.Id), Times.Once);
    }
}
