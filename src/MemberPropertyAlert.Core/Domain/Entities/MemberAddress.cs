using System;
using System.Collections.Generic;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Domain.Entities;

public sealed class MemberAddress : Entity
{
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

    public string TenantId { get; }
    public string InstitutionId { get; }
    public Address Address { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<string> Tags => _tags;
    public DateTimeOffset? LastMatchedAtUtc { get; private set; }
    public string? LastMatchedListingId { get; private set; }

    private MemberAddress(
        string id,
    string tenantId,
        string institutionId,
        Address address,
        bool isActive,
        IEnumerable<string>? tags,
        DateTimeOffset? lastMatchedAtUtc,
        string? lastMatchedListingId,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        TenantId = tenantId;
        InstitutionId = institutionId;
        Address = address;
        IsActive = isActive;
        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                _tags.Add(tag.Trim());
            }
        }

        LastMatchedAtUtc = lastMatchedAtUtc;
        LastMatchedListingId = lastMatchedListingId;
    }

    public static Result<MemberAddress> Create(
        string id,
        string tenantId,
        string institutionId,
        Address address,
        IEnumerable<string>? tags = null,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<MemberAddress>.Failure("Tenant id is required for addresses.");
        }

        if (string.IsNullOrWhiteSpace(institutionId))
        {
            return Result<MemberAddress>.Failure("Institution id is required.");
        }

        return Result<MemberAddress>.Success(new MemberAddress(id, tenantId.Trim(), institutionId, address, isActive, tags, null, null));
    }

    public static MemberAddress Rehydrate(
        string id,
        string tenantId,
        string institutionId,
        Address address,
        IEnumerable<string>? tags,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? lastMatchedAtUtc,
        string? lastMatchedListingId)
    {
        return new MemberAddress(id, tenantId, institutionId, address, isActive, tags, lastMatchedAtUtc, lastMatchedListingId, createdAtUtc, updatedAtUtc);
    }

    public Result Activate()
    {
        IsActive = true;
        Touch();
        return Result.Success();
    }

    public Result Deactivate()
    {
        IsActive = false;
        Touch();
        return Result.Success();
    }

    public Result UpdateAddress(Address address)
    {
        Address = address;
        Touch();
        return Result.Success();
    }

    public void RecordMatch(string listingId, DateTimeOffset matchedAtUtc)
    {
        LastMatchedListingId = listingId;
        LastMatchedAtUtc = matchedAtUtc;
        Touch();
    }

    public void ReplaceTags(IEnumerable<string> tags)
    {
        _tags.Clear();
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _tags.Add(tag.Trim());
            }
        }
        Touch();
    }
}
