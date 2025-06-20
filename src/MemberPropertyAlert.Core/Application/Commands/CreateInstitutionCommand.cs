using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Application.Commands
{
    /// <summary>
    /// Command to create a new institution
    /// </summary>
    public class CreateInstitutionCommand : ICommand<Institution>
    {
        public string Name { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
        public NotificationSettings? NotificationSettings { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Command to update an existing institution
    /// </summary>
    public class UpdateInstitutionCommand : ICommand<Institution>
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
        public NotificationSettings? NotificationSettings { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Command to delete an institution
    /// </summary>
    public class DeleteInstitutionCommand : ICommand
    {
        public string Id { get; set; } = string.Empty;
    }
}
