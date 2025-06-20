using MemberPropertyAlert.Core.Common;

namespace MemberPropertyAlert.Core.Validation
{
    /// <summary>
    /// Interface for entity validation
    /// </summary>
    /// <typeparam name="T">The type to validate</typeparam>
    public interface IValidator<T>
    {
        Task<ValidationResult> ValidateAsync(T entity);
        ValidationResult Validate(T entity);
    }

    /// <summary>
    /// Base validator class with common validation methods
    /// </summary>
    /// <typeparam name="T">The type to validate</typeparam>
    public abstract class BaseValidator<T> : IValidator<T>
    {
        protected readonly List<ValidationError> _errors = new();

        public virtual async Task<ValidationResult> ValidateAsync(T entity)
        {
            return await Task.FromResult(Validate(entity));
        }

        public abstract ValidationResult Validate(T entity);

        protected void AddError(string propertyName, string errorMessage, object? attemptedValue = null)
        {
            _errors.Add(new ValidationError
            {
                PropertyName = propertyName,
                ErrorMessage = errorMessage,
                AttemptedValue = attemptedValue ?? string.Empty
            });
        }

        protected void ValidateRequired(string value, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                AddError(propertyName, $"{propertyName} is required", value);
            }
        }

        protected void ValidateEmail(string email, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                AddError(propertyName, $"{propertyName} is required", email);
                return;
            }

            if (!IsValidEmail(email))
            {
                AddError(propertyName, $"{propertyName} must be a valid email address", email);
            }
        }

        protected void ValidateUrl(string? url, string propertyName, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                if (required)
                {
                    AddError(propertyName, $"{propertyName} is required", url);
                }
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                AddError(propertyName, $"{propertyName} must be a valid HTTP or HTTPS URL", url);
            }
        }

        protected void ValidateStringLength(string value, string propertyName, int minLength = 0, int maxLength = int.MaxValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (minLength > 0)
                {
                    AddError(propertyName, $"{propertyName} must be at least {minLength} characters long", value);
                }
                return;
            }

            if (value.Length < minLength)
            {
                AddError(propertyName, $"{propertyName} must be at least {minLength} characters long", value);
            }

            if (value.Length > maxLength)
            {
                AddError(propertyName, $"{propertyName} must not exceed {maxLength} characters", value);
            }
        }

        protected ValidationResult GetResult()
        {
            return _errors.Any() 
                ? ValidationResult.Failure(_errors.ToList()) 
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
    }
}
