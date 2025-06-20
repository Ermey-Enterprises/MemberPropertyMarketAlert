using MemberPropertyAlert.Core.Common;
using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Repositories
{
    /// <summary>
    /// Repository interface for Institution entities
    /// </summary>
    public interface IInstitutionRepository : IQueryableRepository<Institution>
    {
        Task<Result<Institution>> GetByNameAsync(string name);
        Task<Result<IEnumerable<Institution>>> GetActiveInstitutionsAsync();
        Task<Result<IEnumerable<Institution>>> GetInstitutionsWithNotificationMethodAsync(NotificationDeliveryMethod method);
        Task<Result<bool>> IsNameUniqueAsync(string name, string? excludeId = null);
    }
}
