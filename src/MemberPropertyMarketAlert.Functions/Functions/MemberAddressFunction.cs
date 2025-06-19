using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MemberPropertyMarketAlert.Core.DTOs;
using MemberPropertyMarketAlert.Core.Models;
using MemberPropertyMarketAlert.Core.Services;

namespace MemberPropertyMarketAlert.Functions.Functions;

public class MemberAddressFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<MemberAddressFunction> _logger;

    public MemberAddressFunction(ICosmosDbService cosmosDbService, ILogger<MemberAddressFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [Function("CreateMemberAddressBulk")]
    public async Task<HttpResponseData> CreateMemberAddressBulk(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "members/addresses/bulk")] HttpRequestData req)
    {
        _logger.LogInformation("Processing bulk member address creation request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<BulkMemberAddressRequest>(requestBody);

            if (request == null || string.IsNullOrEmpty(request.InstitutionId) || !request.Addresses.Any())
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request. InstitutionId and Addresses are required.");
                return badRequestResponse;
            }

            var response = new BulkMemberAddressResponse
            {
                TotalProcessed = request.Addresses.Count
            };

            var memberAddresses = new List<MemberAddress>();

            foreach (var addressDto in request.Addresses)
            {
                try
                {
                    var memberAddress = new MemberAddress
                    {
                        InstitutionId = request.InstitutionId,
                        AnonymousReferenceId = addressDto.AnonymousReferenceId,
                        Address = addressDto.Address,
                        City = addressDto.City,
                        State = addressDto.State,
                        ZipCode = addressDto.ZipCode
                    };

                    memberAddress.UpdateNormalizedAddress();
                    memberAddresses.Add(memberAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing address for reference ID: {ReferenceId}", addressDto.AnonymousReferenceId);
                    response.ErrorCount++;
                    response.Errors.Add($"Error processing address for reference ID {addressDto.AnonymousReferenceId}: {ex.Message}");
                }
            }

            if (memberAddresses.Any())
            {
                try
                {
                    var createdAddresses = await _cosmosDbService.CreateMemberAddressesBulkAsync(memberAddresses);
                    response.SuccessCount = createdAddresses.Count();
                    response.CreatedIds = createdAddresses.Select(a => a.Id).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating member addresses in bulk");
                    response.ErrorCount = memberAddresses.Count;
                    response.Errors.Add($"Bulk creation failed: {ex.Message}");
                }
            }

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteStringAsync(JsonConvert.SerializeObject(response));
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk member address creation");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred");
            return errorResponse;
        }
    }

    [Function("GetMemberAddresses")]
    public async Task<HttpResponseData> GetMemberAddresses(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "members/addresses/{institutionId}")] HttpRequestData req,
        string institutionId)
    {
        _logger.LogInformation("Getting member addresses for institution: {InstitutionId}", institutionId);

        try
        {
            if (string.IsNullOrEmpty(institutionId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Institution ID is required");
                return badRequestResponse;
            }

            var memberAddresses = await _cosmosDbService.GetMemberAddressesByInstitutionAsync(institutionId);
            
            var responseData = memberAddresses.Select(ma => new MemberAddressResponse
            {
                Id = ma.Id,
                AnonymousReferenceId = ma.AnonymousReferenceId,
                Address = ma.Address,
                City = ma.City,
                State = ma.State,
                ZipCode = ma.ZipCode,
                CreatedDate = ma.CreatedDate,
                IsActive = ma.IsActive
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonConvert.SerializeObject(responseData));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting member addresses for institution: {InstitutionId}", institutionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred");
            return errorResponse;
        }
    }

    [Function("CreateMemberAddress")]
    public async Task<HttpResponseData> CreateMemberAddress(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "members/addresses")] HttpRequestData req)
    {
        _logger.LogInformation("Creating single member address");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var addressData = JsonConvert.DeserializeObject<dynamic>(requestBody);

            if (addressData == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            var memberAddress = new MemberAddress
            {
                InstitutionId = addressData.institutionId,
                AnonymousReferenceId = addressData.anonymousReferenceId,
                Address = addressData.address,
                City = addressData.city,
                State = addressData.state,
                ZipCode = addressData.zipCode
            };

            var createdAddress = await _cosmosDbService.CreateMemberAddressAsync(memberAddress);

            var responseData = new MemberAddressResponse
            {
                Id = createdAddress.Id,
                AnonymousReferenceId = createdAddress.AnonymousReferenceId,
                Address = createdAddress.Address,
                City = createdAddress.City,
                State = createdAddress.State,
                ZipCode = createdAddress.ZipCode,
                CreatedDate = createdAddress.CreatedDate,
                IsActive = createdAddress.IsActive
            };

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync(JsonConvert.SerializeObject(responseData));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating member address");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred");
            return errorResponse;
        }
    }

    [Function("UpdateMemberAddress")]
    public async Task<HttpResponseData> UpdateMemberAddress(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "members/addresses/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Updating member address: {Id}", id);

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var addressData = JsonConvert.DeserializeObject<dynamic>(requestBody);

            if (addressData == null || string.IsNullOrEmpty((string)addressData.institutionId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body or missing institutionId");
                return badRequestResponse;
            }

            string institutionId = addressData.institutionId;
            var existingAddress = await _cosmosDbService.GetMemberAddressAsync(id, institutionId);

            if (existingAddress == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Member address not found");
                return notFoundResponse;
            }

            // Update properties
            existingAddress.Address = (string?)addressData.address ?? existingAddress.Address;
            existingAddress.City = (string?)addressData.city ?? existingAddress.City;
            existingAddress.State = (string?)addressData.state ?? existingAddress.State;
            existingAddress.ZipCode = (string?)addressData.zipCode ?? existingAddress.ZipCode;
            existingAddress.IsActive = (bool?)addressData.isActive ?? existingAddress.IsActive;

            var updatedAddress = await _cosmosDbService.UpdateMemberAddressAsync(existingAddress);

            var responseData = new MemberAddressResponse
            {
                Id = updatedAddress.Id,
                AnonymousReferenceId = updatedAddress.AnonymousReferenceId,
                Address = updatedAddress.Address,
                City = updatedAddress.City,
                State = updatedAddress.State,
                ZipCode = updatedAddress.ZipCode,
                CreatedDate = updatedAddress.CreatedDate,
                IsActive = updatedAddress.IsActive
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonConvert.SerializeObject(responseData));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member address: {Id}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred");
            return errorResponse;
        }
    }

    [Function("DeleteMemberAddress")]
    public async Task<HttpResponseData> DeleteMemberAddress(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "members/addresses/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Deleting member address: {Id}", id);

        try
        {
            var institutionId = req.Query["institutionId"];
            
            if (string.IsNullOrEmpty(institutionId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("institutionId query parameter is required");
                return badRequestResponse;
            }

            await _cosmosDbService.DeleteMemberAddressAsync(id, institutionId);

            var response = req.CreateResponse(HttpStatusCode.NoContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting member address: {Id}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred");
            return errorResponse;
        }
    }
}
