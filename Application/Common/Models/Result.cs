namespace SaluExamPortal.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();

    protected Result(bool succeeded, string? errorMessage = null, IEnumerable<string>? errors = null)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        if (errors != null)
        {
            Errors = errors;
        }
    }

    public static Result Success() => new(true);
    public static Result Failure(string errorMessage) => new(false, errorMessage, [errorMessage]);
    public static Result Failure(IEnumerable<string> errors) => new(false, string.Join("; ", errors), errors);
}

public class Result<T> : Result
{
    public T? Value { get; init; }

    protected Result(bool succeeded, T? value = default, string? errorMessage = null, IEnumerable<string>? errors = null)
        : base(succeeded, errorMessage, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value);
    public new static Result<T> Failure(string errorMessage) => new(false, default, errorMessage, [errorMessage]);
    public new static Result<T> Failure(IEnumerable<string> errors) => new(false, default, string.Join("; ", errors), errors);
}
