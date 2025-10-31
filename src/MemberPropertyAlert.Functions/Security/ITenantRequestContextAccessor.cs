namespace MemberPropertyAlert.Functions.Security;

public interface ITenantRequestContextAccessor
{
    TenantRequestContext? Current { get; }
    void SetCurrent(TenantRequestContext context);
    void Clear();
}
