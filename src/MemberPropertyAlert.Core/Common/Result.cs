using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Common
{
    /// <summary>
    /// Represents the result of an operation that can succeed or fail
    /// </summary>
    public class Result
    {
        protected Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        public static Result Success() => new(true, string.Empty);
        public static Result Failure(string error) => new(false, error);

        public static Result<T> Success<T>(T value) => new(value, true, string.Empty);
        public static Result<T> Failure<T>(string error) => new(default!, false, error);
    }

    /// <summary>
    /// Represents the result of an operation that returns a value
    /// </summary>
    public class Result<T> : Result
    {
        private readonly T _value;

        protected internal Result(T value, bool isSuccess, string error) : base(isSuccess, error)
        {
            if (isSuccess && value is null)
                throw new ArgumentNullException(nameof(value), "Value cannot be null for successful result");
            _value = value!;
        }

        public T Value => IsSuccess ? _value : throw new InvalidOperationException("Cannot access value of failed result");

        public static implicit operator Result<T>(T value) => Success(value);
    }

    /// <summary>
    /// Represents validation errors
    /// </summary>
    public class ValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public object AttemptedValue { get; set; } = null!;
    }

    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    public class ValidationResult : Result
    {
        public List<ValidationError> ValidationErrors { get; }

        private ValidationResult(bool isSuccess, string error, List<ValidationError> validationErrors) 
            : base(isSuccess, error)
        {
            ValidationErrors = validationErrors ?? new List<ValidationError>();
        }

        public static new ValidationResult Success() => new(true, string.Empty, new List<ValidationError>());
        
        public static ValidationResult Failure(List<ValidationError> validationErrors)
        {
            var error = string.Join("; ", validationErrors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return new(false, error, validationErrors);
        }

        public static ValidationResult Failure(string propertyName, string errorMessage, object attemptedValue = null!)
        {
            var validationError = new ValidationError
            {
                PropertyName = propertyName,
                ErrorMessage = errorMessage,
                AttemptedValue = attemptedValue
            };
            return Failure(new List<ValidationError> { validationError });
        }
    }
}
