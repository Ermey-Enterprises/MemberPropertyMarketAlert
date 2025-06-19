using System.ComponentModel.DataAnnotations;

namespace MemberPropertyMarketAlert.Core.DTOs;

public class MemberAddressDto
{
    [Required]
    public string AnonymousReferenceId { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    public string ZipCode { get; set; } = string.Empty;
}

public class BulkMemberAddressRequest
{
    [Required]
    public string InstitutionId { get; set; } = string.Empty;

    [Required]
    public List<MemberAddressDto> Addresses { get; set; } = new();
}

public class BulkMemberAddressResponse
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> CreatedIds { get; set; } = new();
}

public class MemberAddressResponse
{
    public string Id { get; set; } = string.Empty;
    public string AnonymousReferenceId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; }
}
