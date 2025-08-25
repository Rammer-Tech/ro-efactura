using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RoEFactura.Domain.Entities;
using RoEFactura.Extensions;
using RoEFactura.Models;
using RoEFactura.Repositories;
using UblSharp;

namespace RoEFactura.Services.Processing;

public class UblProcessingService
{
    private readonly IValidator<InvoiceType> _ublValidator;
    private readonly IMapper _mapper;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILogger<UblProcessingService> _logger;

    public UblProcessingService(
        IValidator<InvoiceType> ublValidator,
        IMapper mapper,
        IInvoiceRepository invoiceRepository,
        ILogger<UblProcessingService> logger)
    {
        _ublValidator = ublValidator;
        _mapper = mapper;
        _invoiceRepository = invoiceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes a UBL invoice from XML content
    /// </summary>
    public async Task<ProcessingResult> ProcessInvoiceAsync(string xmlContent, string? anafDownloadId = null)
    {
        try
        {
            _logger.LogInformation("Starting UBL invoice processing");

            // 1. Parse UBL XML
            var ublInvoice = UblSharpExtensions.LoadInvoiceFromXml(xmlContent);
            if (ublInvoice == null)
            {
                _logger.LogError("Failed to parse UBL XML");
                return ProcessingResult.Failed("Failed to parse UBL XML content");
            }

            _logger.LogInformation("UBL XML parsed successfully. Invoice: {InvoiceNumber}", ublInvoice.ID?.Value);

            // 2. Validate against RO_CIUS rules
            var validationResult = await _ublValidator.ValidateAsync(ublInvoice);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("UBL validation failed for invoice {InvoiceNumber}: {Errors}", 
                    ublInvoice.ID?.Value, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                
                return ProcessingResult.Failed(validationResult.Errors);
            }

            _logger.LogInformation("UBL validation passed for invoice {InvoiceNumber}", ublInvoice.ID?.Value);

            // 3. Check for duplicate invoice
            var existingInvoice = await _invoiceRepository.GetByNumberAsync(ublInvoice.ID?.Value ?? "");
            if (existingInvoice != null)
            {
                _logger.LogWarning("Invoice {InvoiceNumber} already exists in database", ublInvoice.ID?.Value);
                return ProcessingResult.Failed($"Invoice with number '{ublInvoice.ID?.Value}' already exists");
            }

            // 4. Map to domain model
            var domainInvoice = await MapToDomainAsync(ublInvoice, xmlContent, anafDownloadId);
            
            _logger.LogInformation("Successfully mapped invoice {InvoiceNumber} to domain model", domainInvoice.Number);

            return ProcessingResult.Success(domainInvoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UBL invoice");
            return ProcessingResult.Failed($"Processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts a domain invoice to UBL format
    /// </summary>
    public async Task<string> ConvertToUblXmlAsync(Invoice domainInvoice)
    {
        try
        {
            _logger.LogInformation("Converting invoice {InvoiceNumber} to UBL XML", domainInvoice.Number);

            // Map to UBL type
            var ublInvoice = _mapper.Map<InvoiceType>(domainInvoice);

            // Validate the generated UBL
            var validationResult = await _ublValidator.ValidateAsync(ublInvoice);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new InvalidOperationException($"Generated UBL is invalid: {errors}");
            }

            // Convert to XML
            var xmlContent = ublInvoice.SaveInvoiceToXml();

            _logger.LogInformation("Successfully converted invoice {InvoiceNumber} to UBL XML", domainInvoice.Number);

            return xmlContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting invoice {InvoiceNumber} to UBL XML", domainInvoice.Number);
            throw;
        }
    }

    /// <summary>
    /// Validates UBL XML without processing
    /// </summary>
    public async Task<ProcessingResult> ValidateUblXmlAsync(string xmlContent)
    {
        try
        {
            _logger.LogInformation("Validating UBL XML");

            // Parse UBL XML
            var ublInvoice = UblSharpExtensions.LoadInvoiceFromXml(xmlContent);
            if (ublInvoice == null)
            {
                return ProcessingResult.Failed("Failed to parse UBL XML content");
            }

            // Validate against RO_CIUS rules
            var validationResult = await _ublValidator.ValidateAsync(ublInvoice);
            
            if (!validationResult.IsValid)
            {
                return ProcessingResult.Failed(validationResult.Errors);
            }

            // Create a temporary domain invoice for return (without persistence)
            var domainInvoice = _mapper.Map<Invoice>(ublInvoice);
            domainInvoice.UblXml = xmlContent;

            return ProcessingResult.Success(domainInvoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating UBL XML");
            return ProcessingResult.Failed($"Validation error: {ex.Message}");
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

    private async Task<Invoice> MapToDomainAsync(InvoiceType ublInvoice, string xmlContent, string? anafDownloadId)
    {
        // Map the main invoice
        var domainInvoice = _mapper.Map<Invoice>(ublInvoice);
        
        // Store the original XML
        domainInvoice.UblXml = xmlContent;
        domainInvoice.AnafDownloadId = anafDownloadId;

        // Handle party relationships - check if parties already exist
        if (ublInvoice.AccountingSupplierParty?.Party != null)
        {
            var sellerVatId = ublInvoice.GetSellerVatId();
            var sellerLegalId = ublInvoice.GetSellerLegalId();
            
            var existingSeller = await FindExistingPartyAsync(sellerVatId, sellerLegalId);
            if (existingSeller != null)
            {
                domainInvoice.Seller = existingSeller;
                domainInvoice.SellerId = existingSeller.Id;
            }
        }

        if (ublInvoice.AccountingCustomerParty?.Party != null)
        {
            var buyerVatId = ublInvoice.GetBuyerVatId();
            var buyerLegalId = ublInvoice.GetBuyerLegalId();
            
            var existingBuyer = await FindExistingPartyAsync(buyerVatId, buyerLegalId);
            if (existingBuyer != null)
            {
                domainInvoice.Buyer = existingBuyer;
                domainInvoice.BuyerId = existingBuyer.Id;
            }
        }

        return domainInvoice;
    }

    private async Task<Party?> FindExistingPartyAsync(string? vatId, string? legalId)
    {
        if (!string.IsNullOrEmpty(vatId))
        {
            var partyByVat = await _invoiceRepository.GetPartyByVatIdAsync(vatId);
            if (partyByVat != null) return partyByVat;
        }

        if (!string.IsNullOrEmpty(legalId))
        {
            var partyByLegal = await _invoiceRepository.GetPartyByLegalIdAsync(legalId);
            if (partyByLegal != null) return partyByLegal;
        }

        return null;
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