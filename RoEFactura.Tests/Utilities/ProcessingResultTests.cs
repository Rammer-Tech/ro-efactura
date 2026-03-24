using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Models;
using Xunit;

namespace RoEFactura.Tests.Utilities;

public class ProcessingResultTests
{
    [Fact]
    public void Success_SetsIsSuccessTrue_AndData()
    {
        var result = ProcessingResult<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("hello");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failed_WithMessage_SetsIsSuccessFalse_AndSingleError()
    {
        var result = ProcessingResult<string>.Failed("something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].ErrorMessage.Should().Be("something went wrong");
        result.Errors[0].PropertyName.Should().Be("General");
    }

    [Fact]
    public void Failed_WithValidationFailures_PreservesAllErrors()
    {
        var failures = new List<ValidationFailure>
        {
            new("Field1", "Error 1") { ErrorCode = "BR-1" },
            new("Field2", "Error 2") { ErrorCode = "BR-2" }
        };

        var result = ProcessingResult<string>.Failed(failures);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(f => f.ErrorCode == "BR-1");
        result.Errors.Should().Contain(f => f.ErrorCode == "BR-2");
    }

    [Fact]
    public void WithWarnings_AppendsWarnings_ToResult()
    {
        var result = ProcessingResult<string>.Success("data")
            .WithWarnings(new[] { "warning 1", "warning 2" });

        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("warning 1");
    }

    [Fact]
    public void Failed_WithEmptyEnumerable_HasNoErrors()
    {
        var result = ProcessingResult<int>.Failed(Enumerable.Empty<ValidationFailure>());

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }
}
