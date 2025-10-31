using System;
using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Results;

public record Result
{
    protected Result(bool isSuccess, string? error, IReadOnlyCollection<string>? validationErrors)
    {
        IsSuccess = isSuccess;
        Error = error;
        ValidationErrors = validationErrors ?? Array.Empty<string>();
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public IReadOnlyCollection<string> ValidationErrors { get; }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error) => new(false, error, null);
    public static Result ValidationFailure(params string[] errors) => new(false, "Validation failed.", errors);

    public void EnsureSuccess()
    {
        if (IsFailure)
        {
            throw new InvalidOperationException(Error ?? "An error occurred.");
        }
    }
}

public sealed record Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? error, IReadOnlyCollection<string>? validationErrors)
        : base(isSuccess, error, validationErrors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static new Result<T> Failure(string error) => new(false, default, error, null);
    public static new Result<T> ValidationFailure(params string[] errors) => new(false, default, "Validation failed.", errors);
}
