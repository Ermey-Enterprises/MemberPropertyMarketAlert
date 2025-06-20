using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Functions.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace MemberPropertyAlert.Functions.Api
{
    public class InstitutionController
    {
        private readonly ILogger<InstitutionController> _logger;
        private readonly ICosmosService _cosmosService;

        public InstitutionController(
            ILogger<InstitutionController> logger,
            ICosmosService cosmosService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
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

            try
            {
                var institutions = await _cosmosService.GetAllInstitutionsAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    Count = institutions.Count,
                    Institutions = institutions.Select(i => new InstitutionResponse(i)).ToList()
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting institutions");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
            }
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

                var institution = new Institution
                {
                    Name = institutionRequest.Name,
                    ContactEmail = institutionRequest.ContactEmail,
                    WebhookUrl = institutionRequest.WebhookUrl
                };

                var createdInstitution = await _cosmosService.CreateInstitutionAsync(institution);

                _logger.LogInformation("Institution created successfully: {InstitutionId}", createdInstitution.Id);

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new InstitutionResponse(createdInstitution));
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

            try
            {
                var institution = await _cosmosService.GetInstitutionAsync(institutionId);
                if (institution == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Institution not found");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new InstitutionResponse(institution));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting institution {InstitutionId}", institutionId);
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
