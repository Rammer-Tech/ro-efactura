using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class InvoiceLineValidatorTests
{
    private readonly InvoiceLineValidator _sut = new();

    private ValidationResult Validate(InvoiceLineType line) => _sut.Validate(line);

    private static InvoiceLineType ValidLine() => InvoiceBuilder.BuildValidLine("1");

    // ── BR-21: Line ID required ──────────────────────────────────────────────

    [Fact]
    public void Br21_MissingLineId_Fails()
    {
        var line = ValidLine();
        line.ID = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-21");
    }

    [Fact]
    public void Br21_EmptyLineId_Fails()
    {
        var line = ValidLine();
        line.ID = new IdentifierType { Value = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-21");
    }

    [Fact]
    public void Br21_PresentLineId_Passes()
    {
        Validate(ValidLine()).Errors.Should().NotContain(e => e.ErrorCode == "BR-21");
    }

    // ── BR-22: Quantity required ─────────────────────────────────────────────

    [Fact]
    public void Br22_MissingQuantity_Fails()
    {
        var line = ValidLine();
        line.InvoicedQuantity = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-22");
    }

    // ── BR-23: Unit code required ────────────────────────────────────────────

    [Fact]
    public void Br23_MissingUnitCode_Fails()
    {
        var line = ValidLine();
        line.InvoicedQuantity = new QuantityType { Value = 1m, unitCode = null };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-23");
    }

    [Fact]
    public void Br23_EmptyUnitCode_Fails()
    {
        var line = ValidLine();
        line.InvoicedQuantity = new QuantityType { Value = 1m, unitCode = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-23");
    }

    // ── BR-24: LineExtensionAmount required ──────────────────────────────────

    [Fact]
    public void Br24_MissingLineExtensionAmount_Fails()
    {
        var line = ValidLine();
        line.LineExtensionAmount = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-24");
    }

    // ── BR-25: Price required ────────────────────────────────────────────────

    [Fact]
    public void Br25_MissingPrice_Fails()
    {
        var line = ValidLine();
        line.Price = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-25");
    }

    // ── BR-26: Item name required ────────────────────────────────────────────

    [Fact]
    public void Br26_MissingItemName_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-26");
    }

    [Fact]
    public void Br26_EmptyItemName_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-26");
    }

    // ── BR-27/28: Price must not be negative ─────────────────────────────────

    [Fact]
    public void Br27_NegativePrice_Fails()
    {
        var line = ValidLine();
        line.Price!.PriceAmount = new AmountType { Value = -1m, currencyID = "RON" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-27");
    }

    [Fact]
    public void Br27_ZeroPrice_Passes()
    {
        var line = ValidLine();
        line.Price!.PriceAmount = new AmountType { Value = 0m, currencyID = "RON" };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "BR-27");
    }

    // ── BR-CO-4: VAT category required ──────────────────────────────────────

    [Fact]
    public void BrCo4_MissingVatCategory_Fails()
    {
        var line = ValidLine();
        line.Item!.ClassifiedTaxCategory = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-4");
    }

    [Fact]
    public void BrCo4_EmptyVatCategoryId_Fails()
    {
        var line = ValidLine();
        line.Item!.ClassifiedTaxCategory![0].ID = new IdentifierType { Value = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-4");
    }

    // ── BR-30: Line period ────────────────────────────────────────────────────

    [Fact]
    public void Br30_ValidLinePeriod_Passes()
    {
        var line = ValidLine();
        line.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                StartDate = new DateType { Value = new DateTime(2024, 1, 1) },
                EndDate = new DateType { Value = new DateTime(2024, 1, 31) }
            }
        };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "BR-30");
    }

    [Fact]
    public void Br30_EndBeforeStart_Fails()
    {
        var line = ValidLine();
        line.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                StartDate = new DateType { Value = new DateTime(2024, 1, 31) },
                EndDate = new DateType { Value = new DateTime(2024, 1, 1) }
            }
        };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-30");
    }

    // ── Romanian length limits ────────────────────────────────────────────────

    [Fact]
    public void RoLineNoteLength_ExactlyThreeHundredChars_Passes()
    {
        var line = ValidLine();
        line.Note = new List<TextType> { new TextType { Value = new string('A', 300) } };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "RO-LINE-NOTE-LENGTH");
    }

    [Fact]
    public void RoLineNoteLength_ThreeHundredOneChars_Fails()
    {
        var line = ValidLine();
        line.Note = new List<TextType> { new TextType { Value = new string('A', 301) } };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "RO-LINE-NOTE-LENGTH");
    }

    [Fact]
    public void RoItemNameLength_ExactlyTwoHundredChars_Passes()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = new string('A', 200) };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "RO-ITEM-NAME-LENGTH");
    }

    [Fact]
    public void RoItemNameLength_TwoHundredOneChars_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = new string('A', 201) };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "RO-ITEM-NAME-LENGTH");
    }

    [Fact]
    public void RoItemDescLength_TwoHundredOneChars_Fails()
    {
        var line = ValidLine();
        line.Item!.Description = new List<TextType>
        {
            new TextType { Value = new string('A', 201) }
        };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "RO-ITEM-DESC-LENGTH");
    }

    // ── Full valid line passes all rules ──────────────────────────────────────

    [Fact]
    public void ValidLine_PassesAllRules()
    {
        Validate(ValidLine()).IsValid.Should().BeTrue();
    }
}
