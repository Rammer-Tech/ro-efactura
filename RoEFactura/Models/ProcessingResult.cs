using FluentValidation.Results;

namespace RoEFactura.Models;

/// <summary>
/// Result wrapper for processing and validation operations.
/// </summary>
public class ProcessingResult<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public List<ValidationFailure> Errors { get; private set; } = new();
    public List<string> Warnings { get; private set; } = new();

    private ProcessingResult() { }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static ProcessingResult<T> Success(T data)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = true,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed result with validation errors.
    /// </summary>
    public static ProcessingResult<T> Failed(IEnumerable<ValidationFailure> errors)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = false,
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// Creates a failed result with a general error message.
    /// </summary>
    public static ProcessingResult<T> Failed(string errorMessage)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = false,
            Errors = new List<ValidationFailure>
            {
                new("General", errorMessage)
            }
        };
    }

    /// <summary>
    /// Adds warnings to the result.
    /// </summary>
    public ProcessingResult<T> WithWarnings(IEnumerable<string> warnings)
    {
        Warnings.AddRange(warnings);
        return this;
    }
}