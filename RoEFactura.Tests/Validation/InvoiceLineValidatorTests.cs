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
    public void LineNote_ExactlyMaxLength_Passes()
    {
        var line = ValidLine();
        line.Note = new List<TextType> { new TextType { Value = new string('A', 300) } };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-L300");
    }

    [Fact]
    public void LineNote_OverMaxLength_Fails()
    {
        var line = ValidLine();
        line.Note = new List<TextType> { new TextType { Value = new string('A', 301) } };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-L300");
    }

    [Fact]
    public void ItemName_ExactlyMaxLength_Passes()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = new string('A', 100) };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-L100");
    }

    [Fact]
    public void ItemName_OverMaxLength_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = new string('A', 101) };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-L100");
    }

    [Fact]
    public void ItemDescription_OverMaxLength_Fails()
    {
        var line = ValidLine();
        line.Item!.Description = new List<TextType>
        {
            new TextType { Value = new string('A', 201) }
        };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-L200");
    }

    // ── VAT category rate rules (BR-S-05/BR-Z-05/BR-E-05/BR-AE-05/BR-IC-05/BR-G-05/BR-O-05) ───

    [Theory]
    [InlineData("S", "21", null)]
    [InlineData("S", "0", "BR-S-05")]
    [InlineData("Z", "0", null)]
    [InlineData("Z", "5", "BR-Z-05")]
    [InlineData("E", "0", null)]
    [InlineData("E", "21", "BR-E-05")]
    [InlineData("AE", "0", null)]
    [InlineData("AE", "21", "BR-AE-05")]
    [InlineData("K", "0", null)]
    [InlineData("K", "21", "BR-IC-05")]
    [InlineData("G", "0", null)]
    [InlineData("G", "21", "BR-G-05")]
    [InlineData("O", null, null)]
    [InlineData("O", "21", "BR-O-05")]
    public void LineVatCategory_RateRules(string category, string? percent, string? expectedErrorCode)
    {
        var line = ValidLine();
        line.Item!.ClassifiedTaxCategory = new List<TaxCategoryType>
        {
            new TaxCategoryType
            {
                ID = new IdentifierType { Value = category },
                Percent = percent == null ? null : new PercentType { Value = decimal.Parse(percent) },
                TaxScheme = new TaxSchemeType { ID = new IdentifierType { Value = "VAT" } }
            }
        };

        var result = Validate(line);
        string[] vatRateCodes = ["BR-S-05", "BR-Z-05", "BR-E-05", "BR-AE-05", "BR-IC-05", "BR-G-05", "BR-O-05"];

        if (expectedErrorCode == null)
        {
            result.Errors.Should().NotContain(e => vatRateCodes.Contains(e.ErrorCode));
        }
        else
        {
            result.Errors.Should().Contain(e => e.ErrorCode == expectedErrorCode);
        }
    }

    // ── Full valid line passes all rules ──────────────────────────────────────

    [Fact]
    public void ValidLine_PassesAllRules()
    {
        Validate(ValidLine()).IsValid.Should().BeTrue();
    }
}
