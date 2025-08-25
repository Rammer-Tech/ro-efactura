using System.IO.Compression;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RoEFactura.Extensions;
using RoEFactura.Models;
using UblSharp;

namespace RoEFactura.Services.Processing;

internal class UblProcessingService : IUblProcessingService
{
    private readonly IValidator<InvoiceType> _ublValidator;
    private readonly ILogger<UblProcessingService> _logger;
    private static readonly object _statsLock = new();
    private static ProcessingStats _globalStats = new();

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
    /// Processes UBL XML content and returns structured result
    /// </summary>
    public async Task<ProcessingResult<InvoiceType>> ProcessInvoiceXmlAsync(byte[] xmlData, string fileName)
    {
        try
        {
            var xmlContent = System.Text.Encoding.UTF8.GetString(xmlData);
            _logger.LogInformation("Processing UBL XML file: {FileName}", fileName);
            
            UpdateStats(stats => stats.TotalProcessed++);
            var result = await ProcessInvoiceAsync(xmlContent);
            
            if (result.IsSuccess)
            {
                UpdateStats(stats => stats.SuccessfullyProcessed++);
            }
            else
            {
                UpdateStats(stats => stats.ValidationErrors++);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing XML file {FileName}", fileName);
            UpdateStats(stats => stats.ProcessingErrors++);
            return ProcessingResult<InvoiceType>.Failed($"Processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates UBL invoice against Romanian RO_CIUS rules
    /// </summary>
    public async Task<ProcessingResult<InvoiceType>> ValidateInvoiceAsync(InvoiceType invoice)
    {
        try
        {
            _logger.LogInformation("Validating invoice {InvoiceNumber}", invoice.ID?.Value);
            
            var validationResult = await _ublValidator.ValidateAsync(invoice);
            
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for invoice {InvoiceNumber}: {Errors}", 
                    invoice.ID?.Value, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                
                return ProcessingResult<InvoiceType>.Failed(validationResult.Errors);
            }

            _logger.LogInformation("Validation passed for invoice {InvoiceNumber}", invoice.ID?.Value);
            return ProcessingResult<InvoiceType>.Success(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invoice {InvoiceNumber}", invoice.ID?.Value);
            return ProcessingResult<InvoiceType>.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts invoice from ZIP archive and processes it
    /// </summary>
    public async Task<ProcessingResult<InvoiceType>> ProcessInvoiceZipAsync(byte[] zipData, string fileName)
    {
        try
        {
            _logger.LogInformation("Processing ZIP file: {FileName}", fileName);
            
            using var stream = new MemoryStream(zipData);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // Look for XML files in the archive
            var xmlEntry = archive.Entries.FirstOrDefault(e => 
                e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

            if (xmlEntry == null)
            {
                return ProcessingResult<InvoiceType>.Failed("No XML file found in ZIP archive");
            }

            using var xmlStream = xmlEntry.Open();
            using var reader = new StreamReader(xmlStream);
            var xmlContent = await reader.ReadToEndAsync();

            _logger.LogInformation("Extracted XML from ZIP: {XmlFileName}", xmlEntry.Name);
            
            return await ProcessInvoiceAsync(xmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZIP file {FileName}", fileName);
            return ProcessingResult<InvoiceType>.Failed($"ZIP processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets processing statistics for monitoring
    /// </summary>
    public ProcessingStats GetProcessingStats()
    {
        lock (_statsLock)
        {
            return new ProcessingStats
            {
                TotalProcessed = _globalStats.TotalProcessed,
                SuccessfullyProcessed = _globalStats.SuccessfullyProcessed,
                ValidationErrors = _globalStats.ValidationErrors,
                ProcessingErrors = _globalStats.ProcessingErrors,
                LastProcessedAt = _globalStats.LastProcessedAt
            };
        }
    }

    /// <summary>
    /// Resets processing statistics
    /// </summary>
    public void ResetProcessingStats()
    {
        lock (_statsLock)
        {
            _globalStats = new ProcessingStats();
            _logger.LogInformation("Processing statistics reset");
        }
    }

    private static void UpdateStats(Action<ProcessingStats> updateAction)
    {
        lock (_statsLock)
        {
            updateAction(_globalStats);
            _globalStats.LastProcessedAt = DateTime.UtcNow;
        }
    }
}

public class ProcessingStats
{
    /// <summary>
    /// Total number of invoices processed
    /// </summary>
    public int TotalProcessed { get; set; }
    
    /// <summary>
    /// Number of successfully processed invoices
    /// </summary>
    public int SuccessfullyProcessed { get; set; }
    
    /// <summary>
    /// Number of validation errors encountered
    /// </summary>
    public int ValidationErrors { get; set; }
    
    /// <summary>
    /// Number of processing errors encountered
    /// </summary>
    public int ProcessingErrors { get; set; }
    
    /// <summary>
    /// Timestamp of last processing operation
    /// </summary>
    public DateTime? LastProcessedAt { get; set; }
    
    /// <summary>
    /// Success rate as a percentage
    /// </summary>
    public double SuccessRate => TotalProcessed > 0 ? (double)SuccessfullyProcessed / TotalProcessed * 100 : 0;
}