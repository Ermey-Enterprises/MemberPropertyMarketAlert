using Microsoft.Extensions.Logging;
using MemberPropertyAlert.Core.Application.Commands;
using MemberPropertyAlert.Core.Application.Queries;
using MemberPropertyAlert.Core.Common;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Core.Validation;

namespace MemberPropertyAlert.Functions.Services
{
    // Command Handlers
    public class CreateInstitutionCommandHandler : ICommandHandler<CreateInstitutionCommand, Institution>
    {
        private readonly ILogger<CreateInstitutionCommandHandler> _logger;
        private readonly ICosmosService _cosmosService;

        public CreateInstitutionCommandHandler(
            ILogger<CreateInstitutionCommandHandler> logger,
            ICosmosService cosmosService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
        }

        public async Task<Result<Institution>> HandleAsync(CreateInstitutionCommand command)
        {
            try
            {
                _logger.LogInformation("Creating institution: {Name}", command.Name);

                var institution = new Institution
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = command.Name,
                    ContactEmail = command.ContactEmail ?? string.Empty,
                    WebhookUrl = command.WebhookUrl,
                    NotificationSettings = command.NotificationSettings,
                    IsActive = command.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdInstitution = await _cosmosService.CreateInstitutionAsync(institution);
                
                _logger.LogInformation("Institution created successfully: {InstitutionId}", createdInstitution.Id);
                return Result<Institution>.Success(createdInstitution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating institution: {Name}", command.Name);
                return Result<Institution>.Failure($"Failed to create institution: {ex.Message}");
            }
        }
    }

    public class UpdateInstitutionCommandHandler : ICommandHandler<UpdateInstitutionCommand, Institution>
    {
        private readonly ILogger<UpdateInstitutionCommandHandler> _logger;
        private readonly ICosmosService _cosmosService;

        public UpdateInstitutionCommandHandler(
            ILogger<UpdateInstitutionCommandHandler> logger,
            ICosmosService cosmosService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
        }

        public async Task<Result<Institution>> HandleAsync(UpdateInstitutionCommand command)
        {
            try
            {
                _logger.LogInformation("Updating institution: {InstitutionId}", command.Id);

                var existingInstitution = await _cosmosService.GetInstitutionAsync(command.Id);
                if (existingInstitution == null)
                {
                    return Result<Institution>.Failure($"Institution with ID {command.Id} not found");
                }

                existingInstitution.Name = command.Name;
                existingInstitution.ContactEmail = command.ContactEmail ?? string.Empty;
                existingInstitution.WebhookUrl = command.WebhookUrl;
                existingInstitution.NotificationSettings = command.NotificationSettings;
                existingInstitution.IsActive = command.IsActive;
                existingInstitution.UpdatedAt = DateTime.UtcNow;

                var updatedInstitution = await _cosmosService.UpdateInstitutionAsync(existingInstitution);
                
                _logger.LogInformation("Institution updated successfully: {InstitutionId}", updatedInstitution.Id);
                return Result<Institution>.Success(updatedInstitution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating institution: {InstitutionId}", command.Id);
                return Result<Institution>.Failure($"Failed to update institution: {ex.Message}");
            }
        }
    }

    public class DeleteInstitutionCommandHandler : ICommandHandler<DeleteInstitutionCommand>
    {
        private readonly ILogger<DeleteInstitutionCommandHandler> _logger;
        private readonly ICosmosService _cosmosService;

        public DeleteInstitutionCommandHandler(
            ILogger<DeleteInstitutionCommandHandler> logger,
            ICosmosService cosmosService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
        }

        public async Task<Result> HandleAsync(DeleteInstitutionCommand command)
        {
            try
            {
                _logger.LogInformation("Deleting institution: {InstitutionId}", command.Id);

                var existingInstitution = await _cosmosService.GetInstitutionAsync(command.Id);
                if (existingInstitution == null)
                {
                    return Result.Failure($"Institution with ID {command.Id} not found");
                }

                await _cosmosService.DeleteInstitutionAsync(command.Id);
                
                _logger.LogInformation("Institution deleted successfully: {InstitutionId}", command.Id);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting institution: {InstitutionId}", command.Id);
                return Result.Failure($"Failed to delete institution: {ex.Message}");
            }
        }
    }

    // Query Handlers
    public class GetAllInstitutionsQueryHandler : IQueryHandler<GetAllInstitutionsQuery, IEnumerable<Institution>>
    {
        private readonly ILogger<GetAllInstitutionsQueryHandler> _logger;
        private readonly ICosmosService _cosmosService;

        public GetAllInstitutionsQueryHandler(
            ILogger<GetAllInstitutionsQueryHandler> logger,
            ICosmosService cosmosService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
        }

        public async Task<Result<IEnumerable<Institution>>> HandleAsync(GetAllInstitutionsQuery query)
        {
            try
            {
                _logger.LogInformation("Getting all institutions (ActiveOnly: {ActiveOnly})", query.ActiveOnly);

                var institutions = await _cosmosService.GetAllInstitutionsAsync();
                
                IEnumerable<Institution> result = institutions;
                if (query.ActiveOnly)
                {
                    result = institutions.Where(i => i.IsActive);
                }

                _logger.LogInformation("Retrieved {Count} institutions", result.Count());
                return Result<IEnumerable<Institution>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting institutions");
                return Result<IEnumerable<Institution>>.Failure($"Failed to get institutions: {ex.Message}");
            }
        }
    }

    public class GetInstitutionByIdQueryHandler : IQueryHandler<GetInstitutionByIdQuery, Institution>
    {
        private readonly ILogger<GetInstitutionByIdQueryHandler> _logger;
        private readonly ICosmosService _cosmosService;

        public GetInstitutionByIdQueryHandler(
            ILogger<GetInstitutionByIdQueryHandler> logger,
            ICosmosService cosmosService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
        }

        public async Task<Result<Institution>> HandleAsync(GetInstitutionByIdQuery query)
        {
            try
            {
                _logger.LogInformation("Getting institution by ID: {InstitutionId}", query.Id);

                var institution = await _cosmosService.GetInstitutionAsync(query.Id);
                
                if (institution == null)
                {
                    return Result<Institution>.Failure($"Institution with ID {query.Id} not found");
                }

                _logger.LogInformation("Retrieved institution: {InstitutionId}", institution.Id);
                return Result<Institution>.Success(institution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting institution by ID: {InstitutionId}", query.Id);
                return Result<Institution>.Failure($"Failed to get institution: {ex.Message}");
            }
        }
    }

    // Validators
    public class CreateInstitutionCommandValidator : IValidator<CreateInstitutionCommand>
    {
        private readonly ILogger<CreateInstitutionCommandValidator> _logger;

        public CreateInstitutionCommandValidator(ILogger<CreateInstitutionCommandValidator> logger)
        {
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateAsync(CreateInstitutionCommand command)
        {
            return await Task.FromResult(Validate(command));
        }

        public ValidationResult Validate(CreateInstitutionCommand command)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(command.Name))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.Name),
                    ErrorMessage = "Institution name is required",
                    AttemptedValue = command.Name
                });
            }

            if (string.IsNullOrWhiteSpace(command.ContactEmail))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.ContactEmail),
                    ErrorMessage = "Contact email is required",
                    AttemptedValue = command.ContactEmail
                });
            }
            else if (!IsValidEmail(command.ContactEmail))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.ContactEmail),
                    ErrorMessage = "Contact email format is invalid",
                    AttemptedValue = command.ContactEmail
                });
            }

            if (!string.IsNullOrWhiteSpace(command.WebhookUrl) && !IsValidUrl(command.WebhookUrl))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.WebhookUrl),
                    ErrorMessage = "Webhook URL format is invalid",
                    AttemptedValue = command.WebhookUrl
                });
            }

            return errors.Any() 
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) 
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }

    public class UpdateInstitutionCommandValidator : IValidator<UpdateInstitutionCommand>
    {
        private readonly ILogger<UpdateInstitutionCommandValidator> _logger;

        public UpdateInstitutionCommandValidator(ILogger<UpdateInstitutionCommandValidator> logger)
        {
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateAsync(UpdateInstitutionCommand command)
        {
            return await Task.FromResult(Validate(command));
        }

        public ValidationResult Validate(UpdateInstitutionCommand command)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(command.Id))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.Id),
                    ErrorMessage = "Institution ID is required",
                    AttemptedValue = command.Id
                });
            }

            if (string.IsNullOrWhiteSpace(command.Name))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.Name),
                    ErrorMessage = "Institution name is required",
                    AttemptedValue = command.Name
                });
            }

            if (string.IsNullOrWhiteSpace(command.ContactEmail))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.ContactEmail),
                    ErrorMessage = "Contact email is required",
                    AttemptedValue = command.ContactEmail
                });
            }
            else if (!IsValidEmail(command.ContactEmail))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.ContactEmail),
                    ErrorMessage = "Contact email format is invalid",
                    AttemptedValue = command.ContactEmail
                });
            }

            if (!string.IsNullOrWhiteSpace(command.WebhookUrl) && !IsValidUrl(command.WebhookUrl))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(command.WebhookUrl),
                    ErrorMessage = "Webhook URL format is invalid",
                    AttemptedValue = command.WebhookUrl
                });
            }

            return errors.Any() 
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var result) 
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }
    }
}
