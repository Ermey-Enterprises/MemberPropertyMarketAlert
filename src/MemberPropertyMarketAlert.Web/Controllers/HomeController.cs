using Microsoft.AspNetCore.Mvc;
using MemberPropertyMarketAlert.Web.Services;
using MemberPropertyMarketAlert.Core.DTOs;

namespace MemberPropertyMarketAlert.Web.Controllers;

public class HomeController : Controller
{
    private readonly IApiClientService _apiClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IApiClientService apiClient, ILogger<HomeController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet]
    public IActionResult AddressManagement()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateAddress(CreateMemberAddressRequest request)
    {
        try
        {
            var result = await _apiClient.CreateMemberAddressAsync(request);
            TempData["Success"] = "Address created successfully!";
            return RedirectToAction("AddressManagement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating address");
            TempData["Error"] = "Failed to create address. Please try again.";
            return RedirectToAction("AddressManagement");
        }
    }

    [HttpPost]
    public async Task<IActionResult> BulkUpload(BulkMemberAddressRequest request)
    {
        try
        {
            var result = await _apiClient.CreateMemberAddressesBulkAsync(request);
            TempData["Success"] = $"Bulk upload completed! Created {result.SuccessCount} addresses.";
            if (result.ErrorCount > 0)
            {
                TempData["Warning"] = $"{result.ErrorCount} addresses failed to create.";
            }
            return RedirectToAction("AddressManagement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk upload");
            TempData["Error"] = "Bulk upload failed. Please try again.";
            return RedirectToAction("AddressManagement");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAddresses(string institutionId)
    {
        try
        {
            var addresses = await _apiClient.GetMemberAddressesAsync(institutionId);
            return Json(addresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving addresses for institution {InstitutionId}", institutionId);
            return Json(new { error = "Failed to retrieve addresses" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAddress(string id, string institutionId)
    {
        try
        {
            var success = await _apiClient.DeleteMemberAddressAsync(id, institutionId);
            if (success)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, error = "Failed to delete address" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting address {Id}", id);
            return Json(new { success = false, error = "Failed to delete address" });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
