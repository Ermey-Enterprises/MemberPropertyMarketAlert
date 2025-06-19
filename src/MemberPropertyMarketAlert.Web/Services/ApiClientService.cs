using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using MemberPropertyMarketAlert.Core.DTOs;

namespace MemberPropertyMarketAlert.Web.Services;

public interface IApiClientService
{
    Task<BulkMemberAddressResponse> CreateMemberAddressesBulkAsync(BulkMemberAddressRequest request);
    Task<List<MemberAddressResponse>> GetMemberAddressesAsync(string institutionId);
    Task<MemberAddressResponse> CreateMemberAddressAsync(CreateMemberAddressRequest request);
    Task<bool> DeleteMemberAddressAsync(string id, string institutionId);
}

public class ApiClientService : IApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _apiSettings;

    public ApiClientService(HttpClient httpClient, IOptions<ApiSettings> apiSettings)
    {
        _httpClient = httpClient;
        _apiSettings = apiSettings.Value;
    }

    public async Task<BulkMemberAddressResponse> CreateMemberAddressesBulkAsync(BulkMemberAddressRequest request)
    {
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/members/addresses/bulk", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<BulkMemberAddressResponse>(responseJson) ?? new BulkMemberAddressResponse();
    }

    public async Task<List<MemberAddressResponse>> GetMemberAddressesAsync(string institutionId)
    {
        var response = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/members/addresses/{institutionId}");
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<List<MemberAddressResponse>>(responseJson) ?? new List<MemberAddressResponse>();
    }

    public async Task<MemberAddressResponse> CreateMemberAddressAsync(CreateMemberAddressRequest request)
    {
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/members/addresses", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<MemberAddressResponse>(responseJson) ?? new MemberAddressResponse();
    }

    public async Task<bool> DeleteMemberAddressAsync(string id, string institutionId)
    {
        var response = await _httpClient.DeleteAsync($"{_apiSettings.BaseUrl}/members/addresses/{id}?institutionId={institutionId}");
        return response.IsSuccessStatusCode;
    }
}

public class ApiSettings
{
    public string BaseUrl { get; set; } = "http://localhost:7071/api";
}

public class CreateMemberAddressRequest
{
    public string InstitutionId { get; set; } = string.Empty;
    public string AnonymousReferenceId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}
