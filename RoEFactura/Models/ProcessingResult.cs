using FluentValidation.Results;
using RoEFactura.Domain.Entities;

namespace RoEFactura.Models;

public class ProcessingResult
{
    public bool IsSuccess { get; private set; }
    public Invoice? Invoice { get; private set; }
    public List<ValidationFailure> Errors { get; private set; } = new();
    public List<string> Warnings { get; private set; } = new();

    private ProcessingResult() { }

    public static ProcessingResult Success(Invoice invoice)
    {
        return new ProcessingResult
        {
            IsSuccess = true,
            Invoice = invoice
        };
    }

    public static ProcessingResult Failed(IEnumerable<ValidationFailure> errors)
    {
        return new ProcessingResult
        {
            IsSuccess = false,
            Errors = errors.ToList()
        };
    }

    public static ProcessingResult Failed(string errorMessage)
    {
        return new ProcessingResult
        {
            IsSuccess = false,
            Errors = new List<ValidationFailure>
            {
                new("General", errorMessage)
            }
        };
    }

    public ProcessingResult WithWarnings(IEnumerable<string> warnings)
    {
        Warnings.AddRange(warnings);
        return this;
    }
}