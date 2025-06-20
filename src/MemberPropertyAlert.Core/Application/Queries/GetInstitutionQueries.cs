using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Application.Queries
{
    /// <summary>
    /// Query to get an institution by ID
    /// </summary>
    public class GetInstitutionByIdQuery : IQuery<Institution>
    {
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query to get all institutions
    /// </summary>
    public class GetAllInstitutionsQuery : IQuery<IEnumerable<Institution>>
    {
        public bool ActiveOnly { get; set; } = false;
    }

    /// <summary>
    /// Query to get an institution by name
    /// </summary>
    public class GetInstitutionByNameQuery : IQuery<Institution>
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query to get institutions with specific notification method
    /// </summary>
    public class GetInstitutionsWithNotificationMethodQuery : IQuery<IEnumerable<Institution>>
    {
        public NotificationDeliveryMethod Method { get; set; }
    }

    /// <summary>
    /// DTO for institution responses
    /// </summary>
    public class InstitutionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
        public NotificationSettings? NotificationSettings { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int TotalMembers { get; set; }
        public int ActiveAlerts { get; set; }
    }

    /// <summary>
    /// Query to get institution summary information
    /// </summary>
    public class GetInstitutionSummaryQuery : IQuery<InstitutionDto>
    {
        public string Id { get; set; } = string.Empty;
    }
}
