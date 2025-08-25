using FluentValidation.Results;

namespace RoEFactura.Models;

public class ProcessingResult<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public List<ValidationFailure> Errors { get; private set; } = new();
    public List<string> Warnings { get; private set; } = new();

    private ProcessingResult() { }

    public static ProcessingResult<T> Success(T data)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = true,
            Data = data
        };
    }

    public static ProcessingResult<T> Failed(IEnumerable<ValidationFailure> errors)
    {
        return new ProcessingResult<T>
        {
            IsSuccess = false,
            Errors = errors.ToList()
        };
    }

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

    public ProcessingResult<T> WithWarnings(IEnumerable<string> warnings)
    {
        Warnings.AddRange(warnings);
        return this;
    }
}