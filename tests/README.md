# Testing Guide

This directory contains comprehensive test suites for the MemberPropertyMarketAlert solution, following industry best practices for .NET testing.

## Test Structure

```
tests/
├── MemberPropertyAlert.Core.Tests/          # Unit tests for core business logic
├── MemberPropertyAlert.Functions.Tests/     # Integration tests for Azure Functions
├── MemberPropertyAlert.Integration.Tests/   # End-to-end integration tests
└── README.md                                # This file
```

## Testing Frameworks

We use the following testing frameworks and libraries:

- **xUnit** - Primary testing framework (Microsoft's recommended choice)
- **FluentAssertions** - More readable and expressive assertions
- **Moq** - Mocking framework for dependencies
- **AutoFixture** - Automatic test data generation
- **Testcontainers** - Docker-based integration testing
- **Coverlet** - Code coverage analysis

## Test Categories

### Unit Tests (`MemberPropertyAlert.Core.Tests`)

Tests individual components in isolation:
- Domain models validation
- Business logic verification
- Result pattern implementation
- Validation logic

**Example:**
```csharp
[Fact]
public void Result_Success_ShouldCreateSuccessfulResult()
{
    // Arrange & Act
    var result = Result.Success();

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.IsFailure.Should().BeFalse();
}
```

### Integration Tests (`MemberPropertyAlert.Functions.Tests`)

Tests component interactions and Azure Functions:
- Command and query handlers
- Service integrations
- Azure Functions endpoints
- Dependency injection

**Example:**
```csharp
[Fact]
public async Task CreateInstitutionCommandHandler_WithValidCommand_ShouldReturnSuccess()
{
    // Arrange
    var command = _fixture.Create<CreateInstitutionCommand>();
    var handler = new CreateInstitutionCommandHandler(_logger.Object, _cosmosService.Object);

    // Act
    var result = await handler.HandleAsync(command);

    // Assert
    result.IsSuccess.Should().BeTrue();
}
```

### End-to-End Tests (`MemberPropertyAlert.Integration.Tests`)

Tests complete workflows:
- API endpoint testing
- Database integration
- External service integration
- Performance testing

## Running Tests

### Using PowerShell Script (Recommended)

```powershell
# Run all tests
.\scripts\Test-Local.ps1

# Run only unit tests
.\scripts\Test-Local.ps1 -TestType Unit

# Run with code coverage
.\scripts\Test-Local.ps1 -TestType Coverage

# Run in Release configuration
.\scripts\Test-Local.ps1 -Configuration Release -Verbose
```

### Using .NET CLI

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/MemberPropertyAlert.Core.Tests/

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run with detailed output
dotnet test --verbosity normal
```

### Using Visual Studio

1. Open the solution in Visual Studio
2. Use Test Explorer (Test → Test Explorer)
3. Run All Tests or select specific tests

## Test Naming Conventions

We follow the **MethodName_StateUnderTest_ExpectedBehavior** pattern:

```csharp
[Fact]
public void CreateInstitution_WithValidData_ShouldReturnSuccess()

[Fact]
public void UpdateInstitution_WhenNotFound_ShouldReturnFailure()

[Fact]
public void ValidateEmail_WithInvalidFormat_ShouldReturnValidationError()
```

## Test Data Management

### AutoFixture for Test Data

```csharp
private readonly IFixture _fixture = new Fixture().Customize(new AutoMoqCustomization());

[Fact]
public void Test_WithGeneratedData()
{
    // Arrange
    var institution = _fixture.Create<Institution>();
    var command = _fixture.Build<CreateInstitutionCommand>()
        .With(x => x.Name, "Test Institution")
        .Create();
}
```

### Test Builders

For complex scenarios, use test builders:

```csharp
public class InstitutionBuilder
{
    private Institution _institution = new();

    public InstitutionBuilder WithName(string name)
    {
        _institution.Name = name;
        return this;
    }

    public InstitutionBuilder WithActiveStatus(bool isActive)
    {
        _institution.IsActive = isActive;
        return this;
    }

    public Institution Build() => _institution;
}
```

## Mocking Guidelines

### Service Dependencies

```csharp
[Fact]
public async Task Handler_ShouldCallService()
{
    // Arrange
    var mockService = new Mock<ICosmosService>();
    mockService.Setup(x => x.CreateInstitutionAsync(It.IsAny<Institution>()))
        .ReturnsAsync(new Institution());

    // Act & Assert
    mockService.Verify(x => x.CreateInstitutionAsync(It.IsAny<Institution>()), Times.Once);
}
```

### Logger Verification

```csharp
// Verify logging calls
_logger.Verify(
    x => x.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Institution created")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
    Times.Once);
```

## Code Coverage

### Running Coverage Analysis

```powershell
# Generate coverage report
.\scripts\Test-Local.ps1 -TestType Coverage

# Coverage files are generated in TestResults/Coverage/
```

### Coverage Targets

- **Minimum**: 80% line coverage
- **Target**: 90% line coverage
- **Critical paths**: 100% coverage for business logic

### Excluding from Coverage

```csharp
[ExcludeFromCodeCoverage]
public class ConfigurationModel
{
    // Configuration POCOs don't need coverage
}
```

## Integration Testing with Testcontainers

For database integration tests:

```csharp
public class CosmosIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbContainer _cosmosContainer;

    public CosmosIntegrationTests()
    {
        _cosmosContainer = new CosmosDbBuilder()
            .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _cosmosContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _cosmosContainer.DisposeAsync();
    }
}
```

## Performance Testing

### Basic Performance Tests

```csharp
[Fact]
public async Task CreateInstitution_ShouldCompleteWithinTimeout()
{
    // Arrange
    var stopwatch = Stopwatch.StartNew();

    // Act
    var result = await handler.HandleAsync(command);

    // Assert
    stopwatch.Stop();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
}
```

## Continuous Integration

The test suite integrates with GitHub Actions:

```yaml
- name: Run Tests
  run: dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    files: '**/coverage.cobertura.xml'
```

## Best Practices

### 1. Test Independence
- Each test should be independent
- Use fresh test data for each test
- Clean up resources after tests

### 2. Arrange-Act-Assert Pattern
```csharp
[Fact]
public void Test_Example()
{
    // Arrange - Set up test data and dependencies
    var input = "test";
    var expected = "TEST";

    // Act - Execute the method under test
    var result = input.ToUpper();

    // Assert - Verify the outcome
    result.Should().Be(expected);
}
```

### 3. Descriptive Test Names
- Use descriptive names that explain the scenario
- Include the expected outcome
- Make tests self-documenting

### 4. Test One Thing
- Each test should verify one specific behavior
- Keep tests focused and simple
- Avoid testing multiple scenarios in one test

### 5. Use Meaningful Assertions
```csharp
// Good
result.Should().NotBeNull();
result.IsSuccess.Should().BeTrue();
result.Value.Should().BeEquivalentTo(expected);

// Avoid
Assert.True(result != null && result.IsSuccess && result.Value.Equals(expected));
```

## Troubleshooting

### Common Issues

1. **Tests fail locally but pass in CI**
   - Check for timezone dependencies
   - Verify file path separators
   - Check for environment-specific configurations

2. **Slow test execution**
   - Review database setup/teardown
   - Check for unnecessary async operations
   - Consider parallel test execution

3. **Flaky tests**
   - Look for timing dependencies
   - Check for shared state between tests
   - Review async/await patterns

### Getting Help

- Check the test output for detailed error messages
- Review the test logs in `TestResults/`
- Use the verbose flag for more detailed output: `.\scripts\Test-Local.ps1 -Verbose`

## Contributing

When adding new tests:

1. Follow the established naming conventions
2. Add appropriate test categories/traits
3. Ensure good code coverage
4. Update this README if adding new test patterns
5. Run the full test suite before submitting PRs

## Resources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Moq Documentation](https://github.com/moq/moq4)
- [AutoFixture Documentation](https://github.com/AutoFixture/AutoFixture)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/best-practices)
