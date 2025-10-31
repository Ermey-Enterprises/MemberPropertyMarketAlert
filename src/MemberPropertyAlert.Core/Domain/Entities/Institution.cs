using System;
using System.Collections.Generic;
using System.Linq;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Domain.Entities;

public sealed class Institution : AggregateRoot
{
    private readonly List<MemberAddress> _addresses = new();

    public string TenantId { get; private set; }
    public string Name { get; private set; }
    public string NormalizedName => Name.ToUpperInvariant();
    public InstitutionStatus Status { get; private set; }
    public IReadOnlyCollection<MemberAddress> Addresses => _addresses.AsReadOnly();
    public string TimeZoneId { get; private set; }
    public string? PrimaryContactEmail { get; private set; }

    private Institution(
        string id,
        string tenantId,
        string name,
        string timeZoneId,
        InstitutionStatus status,
        string? primaryContactEmail,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        TenantId = tenantId;
        Name = name;
        TimeZoneId = timeZoneId;
        Status = status;
        PrimaryContactEmail = primaryContactEmail;
    }

    public static Institution Rehydrate(
        string id,
        string tenantId,
        string name,
        string timeZoneId,
        InstitutionStatus status,
        string? primaryContactEmail,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        IEnumerable<MemberAddress>? addresses)
    {
        var institution = new Institution(id, tenantId, name, timeZoneId, status, primaryContactEmail, createdAtUtc, updatedAtUtc);
        if (addresses is not null)
        {
            foreach (var address in addresses)
            {
                institution._addresses.Add(address);
            }
        }

        return institution;
    }

    public static Result<Institution> Create(
        string id,
        string tenantId,
        string name,
        string timeZoneId,
        InstitutionStatus status = InstitutionStatus.Active,
        string? primaryContactEmail = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<Institution>.Failure("Tenant id is required for institutions.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Institution>.Failure("Institution name is required.");
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return Result<Institution>.Failure("Time zone is required.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result<Institution>.Failure($"Time zone '{timeZoneId}' is not recognized.");
        }

        return Result<Institution>.Success(new Institution(id, tenantId.Trim(), name.Trim(), timeZoneId, status, primaryContactEmail?.Trim()));
    }

    public Result<MemberAddress> AddAddress(MemberAddress address)
    {
        if (_addresses.Any(a => a.Id == address.Id))
        {
            return Result<MemberAddress>.Failure($"Address with id '{address.Id}' already exists.");
        }

        if (address.InstitutionId != Id)
        {
            return Result<MemberAddress>.Failure("Address belongs to a different institution.");
        }

        _addresses.Add(address);
        Touch();
        return Result<MemberAddress>.Success(address);
    }

    public Result RemoveAddress(string addressId)
    {
        var address = _addresses.SingleOrDefault(a => a.Id == addressId);
        if (address is null)
        {
            return Result.Failure("Address not found.");
        }

        _addresses.Remove(address);
        Touch();
        return Result.Success();
    }

    public Result UpdateDetails(string name, string? primaryContactEmail, InstitutionStatus status)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Institution name cannot be empty.");
        }

        Name = name.Trim();
        PrimaryContactEmail = string.IsNullOrWhiteSpace(primaryContactEmail) ? null : primaryContactEmail.Trim();
        Status = status;
        Touch();
        return Result.Success();
    }

}
