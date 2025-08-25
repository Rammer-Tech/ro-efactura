using FluentValidation;
using Microsoft.Extensions.Logging;
using RoEFactura.Extensions;
using RoEFactura.Models;
using UblSharp;

namespace RoEFactura.Services.Processing;

public class UblProcessingService
{
    private readonly IValidator<InvoiceType> _ublValidator;
    private readonly ILogger<UblProcessingService> _logger;

    public UblProcessingService(
        IValidator<InvoiceType> ublValidator,
        ILogger<UblProcessingService> logger)
    {
        _ublValidator = ublValidator;
        _logger = logger;
    }

    /// <summary>
    /// Processes a UBL invoice from XML content
    /// </summary>
    public async Task<ProcessingResult<InvoiceType>> ProcessInvoiceAsync(string xmlContent, string? anafDownloadId = null)
    {
        try
        {
            _logger.LogInformation("Starting UBL invoice processing");

            // 1. Parse UBL XML
            var ublInvoice = UblSharpExtensions.LoadInvoiceFromXml(xmlContent);
            if (ublInvoice == null)
            {
                _logger.LogError("Failed to parse UBL XML");
                return ProcessingResult<InvoiceType>.Failed("Failed to parse UBL XML content");
            }

            _logger.LogInformation("UBL XML parsed successfully. Invoice: {InvoiceNumber}", ublInvoice.ID?.Value);

            // 2. Validate against RO_CIUS rules
            var validationResult = await _ublValidator.ValidateAsync(ublInvoice);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("UBL validation failed for invoice {InvoiceNumber}: {Errors}", 
                    ublInvoice.ID?.Value, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                
                return ProcessingResult<InvoiceType>.Failed(validationResult.Errors);
            }

            _logger.LogInformation("UBL validation passed for invoice {InvoiceNumber}", ublInvoice.ID?.Value);
            
            return ProcessingResult<InvoiceType>.Success(ublInvoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UBL invoice");
            return ProcessingResult<InvoiceType>.Failed($"Processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts a UBL invoice to XML string
    /// </summary>
    public async Task<string> ConvertToUblXmlAsync(InvoiceType ublInvoice)
    {
        try
        {
            _logger.LogInformation("Converting invoice {InvoiceNumber} to UBL XML", ublInvoice.ID?.Value);

            // Validate the UBL before converting
            var validationResult = await _ublValidator.ValidateAsync(ublInvoice);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new InvalidOperationException($"UBL invoice is invalid: {errors}");
            }

            // Convert to XML
            var xmlContent = ublInvoice.SaveInvoiceToXml();

            _logger.LogInformation("Successfully converted invoice {InvoiceNumber} to UBL XML", ublInvoice.ID?.Value);

            return xmlContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting invoice {InvoiceNumber} to UBL XML", ublInvoice.ID?.Value);
            throw;
        }
    }

    /// <summary>
    /// Validates UBL XML without processing
    /// </summary>
    public async Task<ProcessingResult<InvoiceType>> ValidateUblXmlAsync(string xmlContent)
    {
        try
        {
            _logger.LogInformation("Validating UBL XML");

            // Parse UBL XML
            var ublInvoice = UblSharpExtensions.LoadInvoiceFromXml(xmlContent);
            if (ublInvoice == null)
            {
                return ProcessingResult<InvoiceType>.Failed("Failed to parse UBL XML content");
            }

            // Validate against RO_CIUS rules
            var validationResult = await _ublValidator.ValidateAsync(ublInvoice);
            
            if (!validationResult.IsValid)
            {
                return ProcessingResult<InvoiceType>.Failed(validationResult.Errors);
            }

            return ProcessingResult<InvoiceType>.Success(ublInvoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating UBL XML");
            return ProcessingResult<InvoiceType>.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets processing statistics for an invoice
    /// </summary>
    public ProcessingStats GetProcessingStats(InvoiceType ublInvoice)
    {
        return new ProcessingStats
        {
            InvoiceNumber = ublInvoice.ID?.Value ?? "",
            IsRomanianInvoice = ublInvoice.IsRomanianInvoice(),
            InvoiceType = ublInvoice.InvoiceTypeCode?.Value ?? "",
            Currency = ublInvoice.GetCurrencyCode(),
            TotalAmount = ublInvoice.GetTotalAmountDue(),
            LineCount = ublInvoice.InvoiceLine?.Count ?? 0,
            VatBreakdownCount = ublInvoice.TaxTotal?.FirstOrDefault()?.TaxSubtotal?.Count ?? 0,
            ValidationSummary = ublInvoice.GetValidationSummary()
        };
    }
}

public class ProcessingStats
{
    public string InvoiceNumber { get; set; } = "";
    public bool IsRomanianInvoice { get; set; }
    public string InvoiceType { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
    public int VatBreakdownCount { get; set; }
    public string ValidationSummary { get; set; } = "";
}