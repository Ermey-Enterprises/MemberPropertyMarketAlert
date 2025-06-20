using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Functions.Models;

namespace MemberPropertyAlert.Functions.Api
{
    public class AddressController
    {
        private readonly ILogger<AddressController> _logger;
        private readonly ICosmosService _cosmosService;
        private readonly ISignalRService _signalRService;

        public AddressController(
            ILogger<AddressController> logger,
            ICosmosService cosmosService,
            ISignalRService signalRService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
            _signalRService = signalRService;
        }

        [Function("CreateAddress")]
        public async Task<HttpResponseData> CreateAddress(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "institutions/{institutionId}/addresses")] 
            HttpRequestData req,
            string institutionId)
        {
            _logger.LogInformation("Creating address for institution {InstitutionId}", institutionId);

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var addressRequest = JsonSerializer.Deserialize<CreateAddressRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (addressRequest == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body");
                }

                // Validate institution exists
                var institution = await _cosmosService.GetInstitutionAsync(institutionId);
                if (institution == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Institution not found");
                }

                var address = new MemberAddress
                {
                    InstitutionId = institutionId,
                    AnonymousMemberId = addressRequest.AnonymousMemberId,
                    StreetAddress = addressRequest.StreetAddress,
                    City = addressRequest.City,
                    State = addressRequest.State,
                    ZipCode = addressRequest.ZipCode,
                    Priority = addressRequest.Priority ?? "standard",
                    Metadata = addressRequest.Metadata ?? new Dictionary<string, object>()
                };

                var createdAddress = await _cosmosService.CreateAddressAsync(address);

                _logger.LogInformation("Address created successfully: {AddressId}", createdAddress.Id);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new AddressResponse(createdAddress));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating address for institution {InstitutionId}", institutionId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("GetAddresses")]
        public async Task<HttpResponseData> GetAddresses(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "institutions/{institutionId}/addresses")] 
            HttpRequestData req,
            string institutionId)
        {
            _logger.LogInformation("Getting addresses for institution {InstitutionId}", institutionId);

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var priority = query["priority"];
                var isActiveStr = query["isActive"];
                bool? isActive = isActiveStr != null ? bool.Parse(isActiveStr) : null;

                var addresses = await _cosmosService.GetAddressesByFilterAsync(institutionId, priority, isActive);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    InstitutionId = institutionId,
                    Count = addresses.Count,
                    Addresses = addresses.Select(a => new AddressResponse(a)).ToList()
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting addresses for institution {InstitutionId}", institutionId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("GetAddress")]
        public async Task<HttpResponseData> GetAddress(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "addresses/{addressId}")] 
            HttpRequestData req,
            string addressId)
        {
            _logger.LogInformation("Getting address {AddressId}", addressId);

            try
            {
                var address = await _cosmosService.GetAddressAsync(addressId);
                if (address == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Address not found");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new AddressResponse(address));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting address {AddressId}", addressId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("UpdateAddress")]
        public async Task<HttpResponseData> UpdateAddress(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "addresses/{addressId}")] 
            HttpRequestData req,
            string addressId)
        {
            _logger.LogInformation("Updating address {AddressId}", addressId);

            try
            {
                var existingAddress = await _cosmosService.GetAddressAsync(addressId);
                if (existingAddress == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Address not found");
                }

                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var updateRequest = JsonSerializer.Deserialize<UpdateAddressRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (updateRequest == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body");
                }

                // Update fields
                if (!string.IsNullOrEmpty(updateRequest.StreetAddress))
                    existingAddress.StreetAddress = updateRequest.StreetAddress;
                if (!string.IsNullOrEmpty(updateRequest.City))
                    existingAddress.City = updateRequest.City;
                if (!string.IsNullOrEmpty(updateRequest.State))
                    existingAddress.State = updateRequest.State;
                if (!string.IsNullOrEmpty(updateRequest.ZipCode))
                    existingAddress.ZipCode = updateRequest.ZipCode;
                if (!string.IsNullOrEmpty(updateRequest.Priority))
                    existingAddress.Priority = updateRequest.Priority;
                if (updateRequest.IsActive.HasValue)
                    existingAddress.IsActive = updateRequest.IsActive.Value;
                if (updateRequest.Metadata != null)
                    existingAddress.Metadata = updateRequest.Metadata;

                existingAddress.UpdatedAt = DateTime.UtcNow;

                var updatedAddress = await _cosmosService.UpdateAddressAsync(existingAddress);

                _logger.LogInformation("Address updated successfully: {AddressId}", addressId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new AddressResponse(updatedAddress));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating address {AddressId}", addressId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("DeleteAddress")]
        public async Task<HttpResponseData> DeleteAddress(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "addresses/{addressId}")] 
            HttpRequestData req,
            string addressId)
        {
            _logger.LogInformation("Deleting address {AddressId}", addressId);

            try
            {
                var address = await _cosmosService.GetAddressAsync(addressId);
                if (address == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Address not found");
                }

                await _cosmosService.DeleteAddressAsync(addressId);

                _logger.LogInformation("Address deleted successfully: {AddressId}", addressId);

                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address {AddressId}", addressId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("CreateBulkAddresses")]
        public async Task<HttpResponseData> CreateBulkAddresses(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "institutions/{institutionId}/addresses/bulk")] 
            HttpRequestData req,
            string institutionId)
        {
            _logger.LogInformation("Creating bulk addresses for institution {InstitutionId}", institutionId);

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var bulkRequest = JsonSerializer.Deserialize<BulkCreateAddressRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (bulkRequest?.Addresses == null || !bulkRequest.Addresses.Any())
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "No addresses provided");
                }

                // Validate institution exists
                var institution = await _cosmosService.GetInstitutionAsync(institutionId);
                if (institution == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Institution not found");
                }

                var addresses = bulkRequest.Addresses.Select(a => new MemberAddress
                {
                    InstitutionId = institutionId,
                    AnonymousMemberId = a.AnonymousMemberId,
                    StreetAddress = a.StreetAddress,
                    City = a.City,
                    State = a.State,
                    ZipCode = a.ZipCode,
                    Priority = a.Priority ?? "standard",
                    Metadata = a.Metadata ?? new Dictionary<string, object>()
                }).ToList();

                var createdAddresses = await _cosmosService.CreateBulkAddressesAsync(addresses);

                _logger.LogInformation("Bulk addresses created successfully: {Count} addresses", createdAddresses.Count);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    InstitutionId = institutionId,
                    Count = createdAddresses.Count,
                    Addresses = createdAddresses.Select(a => new AddressResponse(a)).ToList()
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bulk addresses for institution {InstitutionId}", institutionId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { Error = message, Timestamp = DateTime.UtcNow });
            return response;
        }
    }
}
