namespace MemberPropertyAlert.Core.Models;

public sealed record InstitutionCounts(
    int Total,
    int Active,
    int Suspended,
    int Disabled,
    int AddressCount,
    int ActiveAddressCount
);
