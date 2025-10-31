using System.Threading;

namespace MemberPropertyAlert.Functions.Security;

public sealed class TenantRequestContextAccessor : ITenantRequestContextAccessor
{
    private static readonly AsyncLocal<TenantRequestContext?> CurrentContext = new();

    public TenantRequestContext? Current => CurrentContext.Value;

    public void SetCurrent(TenantRequestContext context)
    {
        CurrentContext.Value = context;
    }

    public void Clear()
    {
        CurrentContext.Value = null;
    }
}
