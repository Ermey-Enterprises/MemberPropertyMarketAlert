using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using MemberPropertyAlert.Core.Application.Commands;
using MemberPropertyAlert.Core.Application.Queries;
using MemberPropertyAlert.Core.Common;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Validation;
using MemberPropertyAlert.Functions.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace MemberPropertyAlert.Functions.Api
{
    /// <summary>
    /// Institution API controller following SOLID principles and CQRS pattern
    /// </summary>
    public class InstitutionController
    {
        private readonly ILogger<InstitutionController> _logger;
        private readonly ICommandHandler<CreateInstitutionCommand, Institution> _createInstitutionHandler;
        private readonly ICommandHandler<UpdateInstitutionCommand, Institution> _updateInstitutionHandler;
        private readonly ICommandHandler<DeleteInstitutionCommand> _deleteInstitutionHandler;
        private readonly IQueryHandler<GetAllInstitutionsQuery, IEnumerable<Institution>> _getAllInstitutionsHandler;
        private readonly IQueryHandler<GetInstitutionByIdQuery, Institution> _getInstitutionByIdHandler;
        private readonly IValidator<CreateInstitutionCommand> _createInstitutionValidator;
        private readonly IValidator<UpdateInstitutionCommand> _updateInstitutionValidator;

        public InstitutionController(
            ILogger<InstitutionController> logger,
            ICommandHandler<CreateInstitutionCommand, Institution> createInstitutionHandler,
            ICommandHandler<UpdateInstitutionCommand, Institution> updateInstitutionHandler,
            ICommandHandler<DeleteInstitutionCommand> deleteInstitutionHandler,
            IQueryHandler<GetAllInstitutionsQuery, IEnumerable<Institution>> getAllInstitutionsHandler,
            IQueryHandler<GetInstitutionByIdQuery, Institution> getInstitutionByIdHandler,
            IValidator<CreateInstitutionCommand> createInstitutionValidator,
            IValidator<UpdateInstitutionCommand> updateInstitutionValidator)
        {
            _logger = logger;
            _createInstitutionHandler = createInstitutionHandler;
            _updateInstitutionHandler = updateInstitutionHandler;
            _deleteInstitutionHandler = deleteInstitutionHandler;
            _getAllInstitutionsHandler = getAllInstitutionsHandler;
            _getInstitutionByIdHandler = getInstitutionByIdHandler;
            _createInstitutionValidator = createInstitutionValidator;
            _updateInstitutionValidator = updateInstitutionValidator;
        }

        [Function("GetInstitutions")]
        [OpenApiOperation(operationId: "GetInstitutions", tags: new[] { "Institutions" }, Summary = "Get all institutions", Description = "Retrieves a list of all registered financial institutions")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Summary = "List of institutions", Description = "Successfully retrieved institutions")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Summary = "Internal server error", Description = "An error occurred while processing the request")]
        public async Task<HttpResponseData> GetInstitutions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "institutions")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Getting all institutions");

            var query = new GetAllInstitutionsQuery { ActiveOnly = false };
            var result = await _getAllInstitutionsHandler.HandleAsync(query);

            if (result.IsFailure)
            {
                _logger.LogError("Error getting institutions: {Error}", result.Error);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Count = result.Value.Count(),
                Institutions = result.Value.Select(i => new InstitutionResponse(i)).ToList()
            });
            return response;
        }

        [Function("CreateInstitution")]
        [OpenApiOperation(operationId: "CreateInstitution", tags: new[] { "Institutions" }, Summary = "Create a new institution", Description = "Creates a new financial institution in the system")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateInstitutionRequest), Required = true, Description = "Institution details")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(InstitutionResponse), Summary = "Institution created", Description = "Successfully created institution")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(object), Summary = "Bad request", Description = "Invalid request body")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Summary = "Internal server error", Description = "An error occurred while processing the request")]
        public async Task<HttpResponseData> CreateInstitution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "institutions")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Creating new institution");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var institutionRequest = JsonSerializer.Deserialize<CreateInstitutionRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (institutionRequest == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body");
                }

                var command = new CreateInstitutionCommand
                {
                    Name = institutionRequest.Name,
                    ContactEmail = institutionRequest.ContactEmail,
                    WebhookUrl = institutionRequest.WebhookUrl,
                    NotificationSettings = institutionRequest.NotificationSettings,
                    IsActive = true
                };

                // Validate the command
                var validationResult = await _createInstitutionValidator.ValidateAsync(command);
                if (validationResult.IsFailure)
                {
                    return await CreateValidationErrorResponse(req, validationResult);
                }

                // Execute the command
                var result = await _createInstitutionHandler.HandleAsync(command);
                if (result.IsFailure)
                {
                    _logger.LogError("Error creating institution: {Error}", result.Error);
                    return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error);
                }

                _logger.LogInformation("Institution created successfully: {InstitutionId}", result.Value.Id);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new InstitutionResponse(result.Value));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating institution");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("GetInstitution")]
        [OpenApiOperation(operationId: "GetInstitution", tags: new[] { "Institutions" }, Summary = "Get institution by ID", Description = "Retrieves a specific financial institution by its ID")]
        [OpenApiParameter(name: "institutionId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The unique identifier of the institution")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(InstitutionResponse), Summary = "Institution found", Description = "Successfully retrieved institution")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json", bodyType: typeof(object), Summary = "Institution not found", Description = "Institution with the specified ID was not found")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Summary = "Internal server error", Description = "An error occurred while processing the request")]
        public async Task<HttpResponseData> GetInstitution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "institutions/{institutionId}")] 
            HttpRequestData req,
            string institutionId)
        {
            _logger.LogInformation("Getting institution {InstitutionId}", institutionId);

            var query = new GetInstitutionByIdQuery { Id = institutionId };
            var result = await _getInstitutionByIdHandler.HandleAsync(query);

            if (result.IsFailure)
            {
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Institution not found");
                }

                _logger.LogError("Error getting institution {InstitutionId}: {Error}", institutionId, result.Error);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new InstitutionResponse(result.Value));
            return response;
        }

        [Function("UpdateInstitution")]
        [OpenApiOperation(operationId: "UpdateInstitution", tags: new[] { "Institutions" }, Summary = "Update an institution", Description = "Updates an existing financial institution")]
        [OpenApiParameter(name: "institutionId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The unique identifier of the institution")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateInstitutionRequest), Required = true, Description = "Updated institution details")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(InstitutionResponse), Summary = "Institution updated", Description = "Successfully updated institution")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(object), Summary = "Bad request", Description = "Invalid request body")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json", bodyType: typeof(object), Summary = "Institution not found", Description = "Institution with the specified ID was not found")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Summary = "Internal server error", Description = "An error occurred while processing the request")]
        public async Task<HttpResponseData> UpdateInstitution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "institutions/{institutionId}")] 
            HttpRequestData req,
            string institutionId)
        {
            _logger.LogInformation("Updating institution {InstitutionId}", institutionId);

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var updateRequest = JsonSerializer.Deserialize<UpdateInstitutionRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (updateRequest == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body");
                }

                var command = new UpdateInstitutionCommand
                {
                    Id = institutionId,
                    Name = updateRequest.Name,
                    ContactEmail = updateRequest.ContactEmail,
                    WebhookUrl = updateRequest.WebhookUrl,
                    NotificationSettings = updateRequest.NotificationSettings,
                    IsActive = updateRequest.IsActive ?? true
                };

                // Validate the command
                var validationResult = await _updateInstitutionValidator.ValidateAsync(command);
                if (validationResult.IsFailure)
                {
                    return await CreateValidationErrorResponse(req, validationResult);
                }

                // Execute the command
                var result = await _updateInstitutionHandler.HandleAsync(command);
                if (result.IsFailure)
                {
                    if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Institution not found");
                    }

                    _logger.LogError("Error updating institution {InstitutionId}: {Error}", institutionId, result.Error);
                    return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error);
                }

                _logger.LogInformation("Institution updated successfully: {InstitutionId}", institutionId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new InstitutionResponse(result.Value));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating institution {InstitutionId}", institutionId);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
        }

        [Function("DeleteInstitution")]
        [OpenApiOperation(operationId: "DeleteInstitution", tags: new[] { "Institutions" }, Summary = "Delete an institution", Description = "Deletes an existing financial institution")]
        [OpenApiParameter(name: "institutionId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The unique identifier of the institution")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NoContent, contentType: "application/json", bodyType: typeof(object), Summary = "Institution deleted", Description = "Successfully deleted institution")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json", bodyType: typeof(object), Summary = "Institution not found", Description = "Institution with the specified ID was not found")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Summary = "Internal server error", Description = "An error occurred while processing the request")]
        public async Task<HttpResponseData> DeleteInstitution(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "institutions/{institutionId}")] 
            HttpRequestData req,
            string institutionId)
        {
            _logger.LogInformation("Deleting institution {InstitutionId}", institutionId);

            var command = new DeleteInstitutionCommand { Id = institutionId };
            var result = await _deleteInstitutionHandler.HandleAsync(command);

            if (result.IsFailure)
            {
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Institution not found");
                }

                _logger.LogError("Error deleting institution {InstitutionId}: {Error}", institutionId, result.Error);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error);
            }

            _logger.LogInformation("Institution deleted successfully: {InstitutionId}", institutionId);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { Error = message, Timestamp = DateTime.UtcNow });
            return response;
        }

        private async Task<HttpResponseData> CreateValidationErrorResponse(HttpRequestData req, ValidationResult validationResult)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new 
            { 
                Error = "Validation failed", 
                ValidationErrors = validationResult.ValidationErrors,
                Timestamp = DateTime.UtcNow 
            });
            return response;
        }
    }
}
