using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RoEFactura.Services.Processing;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using UblSharp;
using Xunit;

namespace RoEFactura.Tests.Services;

public class UblProcessingServiceTests
{
    private static UblProcessingService CreateWithRealValidator()
    {
        var validator = new RoCiusUblValidator();
        var logger = NullLogger<UblProcessingService>.Instance;
        return new UblProcessingService(validator, logger);
    }

    private static UblProcessingService CreateWithMockValidator(Mock<IValidator<InvoiceType>> mockValidator)
    {
        var logger = NullLogger<UblProcessingService>.Instance;
        return new UblProcessingService(mockValidator.Object, logger);
    }

    private static string LoadXml(string relativePath) => ZipBuilder.LoadFixture(relativePath);

    // ── ProcessInvoiceAsync (string content) ─────────────────────────────────

    [Fact]
    public async Task ProcessInvoiceAsync_ValidXml_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");

        var result = await sut.ProcessInvoiceAsync(xml);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ID!.Value.Should().Be("INV-2024-001");
    }

    [Fact]
    public async Task ProcessInvoiceAsync_MalformedXml_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();

        var result = await sut.ProcessInvoiceAsync("<not-ubl>");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessInvoiceAsync_NullXml_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();

        var result = await sut.ProcessInvoiceAsync(null!);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessInvoiceAsync_InvalidInvoice_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Invalid/invalid-br-ro-010.xml");

        var result = await sut.ProcessInvoiceAsync(xml);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "BR-RO-010");
    }

    [Fact]
    public async Task ProcessInvoiceAsync_SkipValidation_SucceedsEvenIfInvalid()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Invalid/invalid-br-ro-010.xml");

        var result = await sut.ProcessInvoiceAsync(xml, skipValidation: true);

        result.IsSuccess.Should().BeTrue();
    }

    // ── ProcessInvoiceXmlAsync (byte[] data) ─────────────────────────────────

    [Fact]
    public async Task ProcessInvoiceXmlAsync_ValidXmlBytes_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        var result = await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        result.IsSuccess.Should().BeTrue();
    }

    // ── ProcessInvoiceZipAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessInvoiceZipAsync_ZipWithSingleXml_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] zip = ZipBuilder.WithEntries(("invoice.xml", xml));

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeTrue();
        result.Data!.ID!.Value.Should().Be("INV-2024-001");
    }

    [Fact]
    public async Task ProcessInvoiceZipAsync_ZipWithInvoiceAndSemnatura_PicksInvoice()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] zip = ZipBuilder.WithEntries(
            ("invoice.xml", xml),
            ("semnatura.xml", "<semnatura/>")
        );

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeTrue();
        result.Data!.ID!.Value.Should().Be("INV-2024-001");
    }

    [Fact]
    public async Task ProcessInvoiceZipAsync_ZipWithOnlySemnatura_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        byte[] zip = ZipBuilder.WithEntries(("semnatura.xml", "<semnatura/>"));

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Contain("semnatura");
    }

    [Fact]
    public async Task ProcessInvoiceZipAsync_EmptyZip_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        byte[] zip = ZipBuilder.WithEntries(); // no entries

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeFalse();
    }

    // ── ValidateInvoiceAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ValidateInvoiceAsync_ValidInvoice_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        var invoice = InvoiceBuilder.Valid().Build();

        var result = await sut.ValidateInvoiceAsync(invoice);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateInvoiceAsync_InvalidInvoice_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        var invoice = InvoiceBuilder.Valid().WithId("NO-DIGIT").Build();

        var result = await sut.ValidateInvoiceAsync(invoice);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "BR-RO-010");
    }

    // ── ProcessingStats ──────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessingStats_SuccessfulProcess_IncrementsSuccessCount()
    {
        var sut = CreateWithRealValidator();
        sut.ResetProcessingStats();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        var stats = sut.GetProcessingStats();
        stats.TotalProcessed.Should().Be(1);
        stats.SuccessfullyProcessed.Should().Be(1);
        stats.ValidationErrors.Should().Be(0);
    }

    [Fact]
    public async Task ProcessingStats_FailedProcess_IncrementsErrorCount()
    {
        var sut = CreateWithRealValidator();
        sut.ResetProcessingStats();
        string xml = LoadXml("Invalid/invalid-br-ro-010.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        var stats = sut.GetProcessingStats();
        stats.TotalProcessed.Should().Be(1);
        stats.ValidationErrors.Should().Be(1);
        stats.SuccessfullyProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessingStats_Reset_ClearsAllCounters()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);
        await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        sut.ResetProcessingStats();

        var stats = sut.GetProcessingStats();
        stats.TotalProcessed.Should().Be(0);
        stats.SuccessRate.Should().Be(0);
    }

    // ── Validator is called (isolation test with mock) ────────────────────────

    [Fact]
    public async Task ProcessInvoiceAsync_CallsValidatorOnce()
    {
        var mockValidator = new Mock<IValidator<InvoiceType>>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<InvoiceType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var sut = CreateWithMockValidator(mockValidator);
        string xml = LoadXml("Valid/valid-380-ron.xml");

        await sut.ProcessInvoiceAsync(xml, skipValidation: false);

        mockValidator.Verify(v => v.ValidateAsync(It.IsAny<InvoiceType>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInvoiceAsync_SkipValidation_NeverCallsValidator()
    {
        var mockValidator = new Mock<IValidator<InvoiceType>>();
        var sut = CreateWithMockValidator(mockValidator);
        string xml = LoadXml("Valid/valid-380-ron.xml");

        await sut.ProcessInvoiceAsync(xml, skipValidation: true);

        mockValidator.Verify(v => v.ValidateAsync(It.IsAny<InvoiceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
