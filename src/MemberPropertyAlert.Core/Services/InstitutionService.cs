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
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Validation;

namespace MemberPropertyAlert.Core.Services;

public sealed class InstitutionService : IInstitutionService
{
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IMemberAddressRepository _memberAddressRepository;

    public InstitutionService(IInstitutionRepository institutionRepository, IMemberAddressRepository memberAddressRepository)
    {
        _institutionRepository = institutionRepository;
        _memberAddressRepository = memberAddressRepository;
    }

    public Task<PagedResult<Institution>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => _institutionRepository.ListAsync(pageNumber, pageSize, cancellationToken);

    public Task<Result<Institution?>> GetAsync(string id, CancellationToken cancellationToken = default)
        => _institutionRepository.GetAsync(id, cancellationToken);

    public async Task<Result<Institution>> CreateAsync(string name, string apiKeyHash, string timeZoneId, string? primaryContactEmail, CancellationToken cancellationToken = default)
    {
        var result = Institution.Create(Guid.NewGuid().ToString("N"), name, apiKeyHash, timeZoneId, InstitutionStatus.Active, primaryContactEmail);
        if (result.IsFailure || result.Value is null)
        {
            return Result<Institution>.Failure(result.Error ?? "Unable to create institution.");
        }

        var createResult = await _institutionRepository.CreateAsync(result.Value, cancellationToken);
        return createResult;
    }

    public async Task<Result> UpdateAsync(string id, string name, string? primaryContactEmail, InstitutionStatus status, CancellationToken cancellationToken = default)
    {
        var existingResult = await _institutionRepository.GetAsync(id, cancellationToken);
        if (existingResult.IsFailure)
        {
            return existingResult;
        }

        var institution = existingResult.Value;
        if (institution is null)
        {
            return Result.Failure("Institution not found.");
        }

        var updateResult = institution.UpdateDetails(name, primaryContactEmail, status);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        return await _institutionRepository.UpdateAsync(institution, cancellationToken);
    }

    public Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
        => _institutionRepository.DeleteAsync(id, cancellationToken);

    public async Task<Result<MemberAddress>> AddAddressAsync(string institutionId, Address address, IEnumerable<string>? tags, CancellationToken cancellationToken = default)
    {
        var institutionResult = await _institutionRepository.GetAsync(institutionId, cancellationToken);
        if (institutionResult.IsFailure)
        {
            return Result<MemberAddress>.Failure(institutionResult.Error ?? "Unable to retrieve institution.");
        }

        var institution = institutionResult.Value;
        if (institution is null)
        {
            return Result<MemberAddress>.Failure("Institution not found.");
        }

        var addressResult = MemberAddress.Create(Guid.NewGuid().ToString("N"), institution.Id, address, tags);
        if (addressResult.IsFailure || addressResult.Value is null)
        {
            return Result<MemberAddress>.Failure(addressResult.Error ?? "Unable to create address.");
        }

        var addResult = institution.AddAddress(addressResult.Value);
        if (addResult.IsFailure)
        {
            return addResult;
        }

        var repoResult = await _memberAddressRepository.CreateAsync(addressResult.Value, cancellationToken);
        if (repoResult.IsFailure)
        {
            return Result<MemberAddress>.Failure(repoResult.Error ?? "Unable to persist address.");
        }

        await _institutionRepository.UpdateAsync(institution, cancellationToken);
        return Result<MemberAddress>.Success(addressResult.Value);
    }

    public Task<Result> RemoveAddressAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
        => _memberAddressRepository.DeleteAsync(institutionId, addressId, cancellationToken);

    public async Task<Result> UpsertAddressesBulkAsync(string institutionId, IEnumerable<MemberAddress> addresses, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(addresses, nameof(addresses));
        var addressList = addresses.ToList();
        Guard.AgainstEmpty(addressList, nameof(addresses));
        return await _memberAddressRepository.UpsertBulkAsync(institutionId, addressList, cancellationToken);
    }
}
