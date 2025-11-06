using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;
using MemberPropertyAlert.Functions.Functions;
using MemberPropertyAlert.Functions.Infrastructure.Telemetry;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MemberPropertyAlert.Functions.Tests;

public sealed class ScanSchedulerFunctionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenScheduleDue_StartsScansForEachTenantState()
    {
        // Arrange
        var scheduleDefinition = CreateScheduleDefinition("0 */5 * * * *", DateTimeOffset.UtcNow.AddMinutes(-10));
        var scheduleServiceMock = new Mock<IScheduleService>();
        scheduleServiceMock
            .Setup(service => service.GetScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CronScheduleDefinition>.Success(scheduleDefinition));

        var scheduleRepositoryMock = new Mock<IScanScheduleRepository>();
        scheduleRepositoryMock
            .Setup(repository => repository.UpsertAsync(It.IsAny<CronScheduleDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CronScheduleDefinition>.Success(scheduleDefinition));

        var tenantContextAccessor = new TenantRequestContextAccessor();

        var institutions = CreateInstitutions();
        var adminContexts = new List<bool?>();
        var institutionRepositoryMock = new Mock<IInstitutionRepository>();
        institutionRepositoryMock
            .Setup(repository => repository.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, int, CancellationToken>((pageNumber, pageSize, _) =>
            {
                adminContexts.Add(tenantContextAccessor.Current?.IsPlatformAdmin);
                if (pageNumber == 1)
                {
                    return Task.FromResult(new PagedResult<Institution>(institutions, institutions.Count, pageNumber, pageSize));
                }

                return Task.FromResult(new PagedResult<Institution>(Array.Empty<Institution>(), 0, pageNumber, pageSize));
            });

        var orchestratorCalls = new List<(string State, TenantRequestContext? Context)>();
        var orchestratorMock = new Mock<IScanOrchestrator>();
        orchestratorMock
            .Setup(orchestrator => orchestrator.StartScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success())
            .Callback<string, CancellationToken>((state, _) =>
            {
                orchestratorCalls.Add((state, tenantContextAccessor.Current));
            });

        var auditEvents = new List<(string Action, IReadOnlyDictionary<string, string?>? Properties, TenantRequestContext? Context)>();
        var auditLoggerMock = new Mock<IAuditLogger>();
        auditLoggerMock
            .Setup(logger => logger.TrackEventAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string?>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<string, IReadOnlyDictionary<string, string?>?, CancellationToken>((action, properties, _) =>
            {
                auditEvents.Add((action, properties, tenantContextAccessor.Current));
            });

        var logEvents = new List<LogEvent>();
        var logStreamPublisherMock = new Mock<ILogStreamPublisher>();
        logStreamPublisherMock
            .Setup(publisher => publisher.PublishAsync(It.IsAny<LogEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<LogEvent, CancellationToken>((logEvent, _) =>
            {
                logEvents.Add(logEvent);
            });

        var function = new ScanSchedulerFunction(
            scheduleServiceMock.Object,
            scheduleRepositoryMock.Object,
            institutionRepositoryMock.Object,
            orchestratorMock.Object,
            tenantContextAccessor,
            auditLoggerMock.Object,
            logStreamPublisherMock.Object,
            NullLogger<ScanSchedulerFunction>.Instance);

        var now = DateTimeOffset.UtcNow;

        // Act
        await function.ExecuteAsync(now, CancellationToken.None);

        // Assert
        Assert.All(adminContexts, context => Assert.True(context));
        Assert.Equal(3, orchestratorCalls.Count);

        var tenantGroups = orchestratorCalls.GroupBy(call => call.Context!.TenantId);
        var tenantOneStates = tenantGroups.Single(group => group.Key == "tenant-one").Select(call => call.State).OrderBy(state => state).ToArray();
        Assert.Equal(new[] { "CA", "WA" }, tenantOneStates);
        var tenantTwoStates = tenantGroups.Single(group => group.Key == "tenant-two").Select(call => call.State).OrderBy(state => state).ToArray();
        Assert.Equal(new[] { "OR" }, tenantTwoStates);

        Assert.All(orchestratorCalls, call =>
        {
            Assert.NotNull(call.Context);
            Assert.False(call.Context!.IsPlatformAdmin);
        });

        Assert.True(auditEvents.Count >= orchestratorCalls.Count);
        Assert.NotEmpty(logEvents);

        scheduleRepositoryMock.Verify(repository => repository.UpsertAsync(It.Is<CronScheduleDefinition>(definition => definition.LastRunUtc.HasValue), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(scheduleDefinition.LastRunUtc);
        Assert.Null(tenantContextAccessor.Current);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleNotDue_DoesNotStartScans()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var scheduleDefinition = CreateScheduleDefinition("0 */5 * * * *", now.AddMinutes(-1));

        var scheduleServiceMock = new Mock<IScheduleService>();
        scheduleServiceMock
            .Setup(service => service.GetScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CronScheduleDefinition>.Success(scheduleDefinition));

        var scheduleRepositoryMock = new Mock<IScanScheduleRepository>();
        var institutionRepositoryMock = new Mock<IInstitutionRepository>(MockBehavior.Strict);
        var orchestratorMock = new Mock<IScanOrchestrator>(MockBehavior.Strict);
        var auditLoggerMock = new Mock<IAuditLogger>(MockBehavior.Strict);
        var logStreamPublisherMock = new Mock<ILogStreamPublisher>(MockBehavior.Strict);
        var tenantContextAccessor = new TenantRequestContextAccessor();

        var function = new ScanSchedulerFunction(
            scheduleServiceMock.Object,
            scheduleRepositoryMock.Object,
            institutionRepositoryMock.Object,
            orchestratorMock.Object,
            tenantContextAccessor,
            auditLoggerMock.Object,
            logStreamPublisherMock.Object,
            NullLogger<ScanSchedulerFunction>.Instance);

        // Act
        await function.ExecuteAsync(now, CancellationToken.None);

        // Assert
        scheduleRepositoryMock.Verify(repository => repository.UpsertAsync(It.IsAny<CronScheduleDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        institutionRepositoryMock.Verify(repository => repository.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        orchestratorMock.Verify(orchestrator => orchestrator.StartScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(tenantContextAccessor.Current);
    }

    private static CronScheduleDefinition CreateScheduleDefinition(string expression, DateTimeOffset? lastRunUtc = null)
    {
        var scheduleResult = CronScheduleDefinition.Create(expression, "UTC", lastRunUtc);
        if (!scheduleResult.IsSuccess || scheduleResult.Value is null)
        {
            throw new InvalidOperationException($"Unable to create schedule definition for expression '{expression}'.");
        }

        return scheduleResult.Value;
    }

    private static List<Institution> CreateInstitutions()
    {
        var tenantOneInstitutionA = CreateInstitution("institution-a", "tenant-one", new[] { "WA", "CA" });
        var tenantOneInstitutionB = CreateInstitution("institution-b", "tenant-one", new[] { "WA" });
        var tenantTwoInstitution = CreateInstitution("institution-c", "tenant-two", new[] { "OR" });

        return new List<Institution> { tenantOneInstitutionA, tenantOneInstitutionB, tenantTwoInstitution };
    }

    private static Institution CreateInstitution(string id, string tenantId, IEnumerable<string> states)
    {
        var institutionResult = Institution.Create(id, tenantId, $"Institution {id}", "UTC");
        if (institutionResult.IsFailure || institutionResult.Value is null)
        {
            throw new InvalidOperationException($"Unable to create institution {id}.");
        }

        var institution = institutionResult.Value;
        foreach (var state in states)
        {
            var address = Address.Create("123 Main St", null, "Springfield", state, "12345", "US");
            var addressResult = MemberAddress.Create(Guid.NewGuid().ToString("N"), tenantId, institution.Id, address);
            if (addressResult.IsFailure || addressResult.Value is null)
            {
                throw new InvalidOperationException($"Unable to create address for institution {id}.");
            }

            var addResult = institution.AddAddress(addressResult.Value);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Unable to add address to institution {id}: {addResult.Error}");
            }
        }

        return institution;
    }
}
