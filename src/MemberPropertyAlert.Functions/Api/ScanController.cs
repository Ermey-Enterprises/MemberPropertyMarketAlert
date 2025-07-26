using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using MemberPropertyAlert.Functions.Services;

namespace MemberPropertyAlert.Functions.Api;

/// <summary>
/// ScanController - Handles all scan-related API endpoints for the UI
/// Force deployment trigger - Updated at 2025-01-26 13:41
/// </summary>

public class ScanController
{
    private readonly ILogger<ScanController> _logger;
    private readonly CosmosService _cosmosService;
    private readonly PropertyScanService _propertyScanService;
    private readonly NotificationService _notificationService;
    private readonly SchedulingService _schedulingService;

    public ScanController(
        ILogger<ScanController> logger,
        CosmosService cosmosService,
        PropertyScanService propertyScanService,
        NotificationService notificationService,
        SchedulingService schedulingService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
        _propertyScanService = propertyScanService;
        _notificationService = notificationService;
        _schedulingService = schedulingService;
    }

    /// <summary>
    /// Start a manual scan for a specific institution
    /// </summary>
    [Function("StartManualScan")]
    public async Task<HttpResponseData> StartManualScan(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scan/start")] HttpRequestData req)
    {
        _logger.LogInformation("StartManualScan function executed");

        try
        {
            // Get institution ID from query parameters or headers
            string? institutionId = req.Query["institutionId"];
            if (string.IsNullOrEmpty(institutionId))
            {
                _logger.LogWarning("Institution ID not provided in request");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Institution ID is required" }));
                return badRequestResponse;
            }

            _logger.LogInformation("Starting manual scan for institution {InstitutionId}", institutionId);

            // Check if there's already an active scan
            var activeScan = await _cosmosService.GetActiveScanLogAsync(institutionId);
            if (activeScan != null)
            {
                _logger.LogWarning("Active scan already exists for institution {InstitutionId}: {ScanId}", 
                    institutionId, activeScan.Id);
                
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                    error = "An active scan is already running for this institution",
                    activeScanId = activeScan.Id
                }));
                return conflictResponse;
            }

            // Create a manual scan request object
            var manualScanRequest = new MemberPropertyAlert.Core.Services.ManualScanRequest
            {
                Priority = "Normal",
                ForceRescan = false
            };

            // Trigger the manual scan using the scheduling service
            var scanLog = await _schedulingService.TriggerManualScanAsync(institutionId, manualScanRequest);

            _logger.LogInformation("Manual scan started successfully: {ScanId}", scanLog.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponse = new
            {
                ScanId = scanLog.Id,
                InstitutionId = scanLog.InstitutionId,
                ScanType = scanLog.ScanType.ToString(),
                Status = scanLog.ScanStatus.ToString(),
                StartedAt = scanLog.StartedAt,
                CompletedAt = scanLog.CompletedAt,
                AddressesScanned = scanLog.AddressesScanned,
                AlertsGenerated = scanLog.AlertsGenerated,
                ApiCallsMade = scanLog.ApiCallsMade,
                ErrorsEncountered = scanLog.ErrorsEncountered,
                ErrorMessage = scanLog.ErrorMessage
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual scan");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while starting scan",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Stop an active scan
    /// </summary>
    [Function("StopScan")]
    public async Task<HttpResponseData> StopScan(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scan/stop")] HttpRequestData req)
    {
        _logger.LogInformation("StopScan function executed");

        try
        {
            string? scanId = req.Query["scanId"];
            if (string.IsNullOrEmpty(scanId))
            {
                _logger.LogWarning("Scan ID not provided in request");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Scan ID is required" }));
                return badRequestResponse;
            }

            var scanLog = await _cosmosService.GetScanLogAsync(scanId);
            if (scanLog == null)
            {
                _logger.LogWarning("Scan not found: {ScanId}", scanId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Scan not found" }));
                return notFoundResponse;
            }

            // Update scan status to completed (since there's no Stopped status)
            scanLog.ScanStatus = MemberPropertyAlert.Core.Models.ScanStatus.Completed;
            scanLog.CompletedAt = DateTime.UtcNow;
            scanLog.ErrorMessage = "Scan stopped by user request";

            await _cosmosService.UpdateScanLogAsync(scanLog);

            _logger.LogInformation("Scan stopped successfully: {ScanId}", scanId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponse = new
            {
                ScanId = scanLog.Id,
                InstitutionId = scanLog.InstitutionId,
                ScanType = scanLog.ScanType.ToString(),
                Status = scanLog.ScanStatus.ToString(),
                StartedAt = scanLog.StartedAt,
                CompletedAt = scanLog.CompletedAt,
                AddressesScanned = scanLog.AddressesScanned,
                AlertsGenerated = scanLog.AlertsGenerated,
                ApiCallsMade = scanLog.ApiCallsMade,
                ErrorsEncountered = scanLog.ErrorsEncountered,
                ErrorMessage = scanLog.ErrorMessage
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping scan");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while stopping scan",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Get scan schedule for an institution
    /// </summary>
    [Function("GetScanSchedule")]
    public async Task<HttpResponseData> GetScanSchedule(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "scan/schedule")] HttpRequestData req)
    {
        _logger.LogInformation("GetScanSchedule function executed");

        try
        {
            string? institutionId = req.Query["institutionId"];
            
            // Get schedules from the scheduling service
            var schedules = await _schedulingService.GetSchedulesAsync(institutionId ?? "default");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            // Return mock schedule data for now
            var scheduleResponse = new
            {
                schedules = new[]
                {
                    new
                    {
                        id = "schedule-1",
                        name = "Daily Property Scan",
                        cronExpression = "0 0 9 * * *",
                        isActive = true,
                        institutionId = institutionId ?? "default",
                        lastRun = DateTime.UtcNow.AddDays(-1),
                        nextRun = DateTime.UtcNow.AddDays(1),
                        createdAt = DateTime.UtcNow.AddDays(-30)
                    }
                },
                totalCount = 1
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scheduleResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan schedule");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while getting scan schedule",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Update scan schedule for an institution
    /// </summary>
    [Function("UpdateScanSchedule")]
    public async Task<HttpResponseData> UpdateScanSchedule(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "scan/schedule")] HttpRequestData req)
    {
        _logger.LogInformation("UpdateScanSchedule function executed");

        try
        {
            string? institutionId = req.Query["institutionId"];
            if (string.IsNullOrEmpty(institutionId))
            {
                _logger.LogWarning("Institution ID not provided in request");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Institution ID is required" }));
                return badRequestResponse;
            }

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Request body is required" }));
                return badRequestResponse;
            }

            _logger.LogInformation("Updating scan schedule for institution {InstitutionId}", institutionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            // Return mock updated schedule data
            var scheduleResponse = new
            {
                id = "schedule-1",
                name = "Updated Daily Property Scan",
                cronExpression = "0 0 9 * * *",
                isActive = true,
                institutionId = institutionId,
                nextRun = DateTime.UtcNow.AddDays(1),
                lastRun = DateTime.UtcNow.AddDays(-1),
                updatedAt = DateTime.UtcNow
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scheduleResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scan schedule");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while updating scan schedule",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Get scan statistics
    /// </summary>
    [Function("GetScanStats")]
    public async Task<HttpResponseData> GetScanStats(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "scan/stats")] HttpRequestData req)
    {
        _logger.LogInformation("GetScanStats function executed");

        try
        {
            string? institutionId = req.Query["institutionId"];
            
            // Get scan statistics from Cosmos service
            var stats = await _cosmosService.GetScanStatisticsAsync(institutionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var statsResponse = new
            {
                totalScans = stats.TotalScans,
                totalAddressesScanned = stats.TotalAddressesScanned,
                totalAlertsGenerated = stats.TotalAlertsGenerated,
                totalApiCalls = stats.TotalApiCalls,
                totalErrors = stats.TotalErrors,
                averageScanDuration = stats.AverageScanDuration.TotalMinutes,
                lastScanAt = stats.LastScanAt,
                statusBreakdown = stats.StatusBreakdown,
                errorBreakdown = stats.ErrorBreakdown,
                successRate = stats.TotalScans > 0 ? 
                    Math.Round((double)(stats.TotalScans - stats.TotalErrors) / stats.TotalScans * 100, 2) : 100.0
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(statsResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan statistics");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while getting scan statistics",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Get scan history for an institution
    /// </summary>
    [Function("GetScanHistory")]
    public async Task<HttpResponseData> GetScanHistory(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "scan/history")] HttpRequestData req)
    {
        _logger.LogInformation("GetScanHistory function executed");

        try
        {
            string? institutionId = req.Query["institutionId"];
            string? limitStr = req.Query["limit"];
            
            int limit = 50; // default
            if (!string.IsNullOrEmpty(limitStr) && int.TryParse(limitStr, out int parsedLimit))
            {
                limit = Math.Min(parsedLimit, 100); // cap at 100
            }

            var scanLogs = !string.IsNullOrEmpty(institutionId)
                ? await _cosmosService.GetScanLogsByInstitutionAsync(institutionId, limit)
                : await _cosmosService.GetRecentScanLogsAsync(limit);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponses = scanLogs.Select(log => new
            {
                ScanId = log.Id,
                InstitutionId = log.InstitutionId,
                ScanType = log.ScanType.ToString(),
                Status = log.ScanStatus.ToString(),
                StartedAt = log.StartedAt,
                CompletedAt = log.CompletedAt,
                AddressesScanned = log.AddressesScanned,
                AlertsGenerated = log.AlertsGenerated,
                ApiCallsMade = log.ApiCallsMade,
                ErrorsEncountered = log.ErrorsEncountered,
                ErrorMessage = log.ErrorMessage
            }).ToList();
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponses));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan history");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while getting scan history",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Get the status of a specific scan
    /// </summary>
    [Function("GetScanStatus")]
    public async Task<HttpResponseData> GetScanStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "scan/{scanId}/status")] HttpRequestData req,
        string scanId)
    {
        _logger.LogInformation("GetScanStatus function executed for scan {ScanId}", scanId);

        try
        {
            var scanLog = await _cosmosService.GetScanLogAsync(scanId);
            if (scanLog == null)
            {
                _logger.LogWarning("Scan not found: {ScanId}", scanId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Scan not found" }));
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponse = new
            {
                ScanId = scanLog.Id,
                InstitutionId = scanLog.InstitutionId,
                ScanType = scanLog.ScanType.ToString(),
                Status = scanLog.ScanStatus.ToString(),
                StartedAt = scanLog.StartedAt,
                CompletedAt = scanLog.CompletedAt,
                AddressesScanned = scanLog.AddressesScanned,
                AlertsGenerated = scanLog.AlertsGenerated,
                ApiCallsMade = scanLog.ApiCallsMade,
                ErrorsEncountered = scanLog.ErrorsEncountered,
                ErrorMessage = scanLog.ErrorMessage
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan status for {ScanId}", scanId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while getting scan status",
                details = ex.Message
            }));
            return errorResponse;
        }
    }
}
