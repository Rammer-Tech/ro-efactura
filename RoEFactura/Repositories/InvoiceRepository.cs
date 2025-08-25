using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoEFactura.Domain.Entities;
using RoEFactura.Infrastructure.Data;

namespace RoEFactura.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly InvoiceDbContext _context;
    private readonly ILogger<InvoiceRepository> _logger;

    public InvoiceRepository(InvoiceDbContext context, ILogger<InvoiceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .Include(i => i.Payee)
            .Include(i => i.Lines)
                .ThenInclude(l => l.AllowanceCharges)
            .Include(i => i.VatBreakdowns)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Invoice?> GetByNumberAsync(string invoiceNumber)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .Include(i => i.Payee)
            .Include(i => i.Lines)
                .ThenInclude(l => l.AllowanceCharges)
            .Include(i => i.VatBreakdowns)
            .FirstOrDefaultAsync(i => i.Number == invoiceNumber);
    }

    public async Task<Invoice?> GetByAnafDownloadIdAsync(string anafDownloadId)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .Include(i => i.Payee)
            .Include(i => i.Lines)
                .ThenInclude(l => l.AllowanceCharges)
            .Include(i => i.VatBreakdowns)
            .FirstOrDefaultAsync(i => i.AnafDownloadId == anafDownloadId);
    }

    public async Task<List<Invoice>> GetBySellerVatIdAsync(string vatId)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .Include(i => i.Payee)
            .Where(i => i.Seller.VatIdentifier == vatId)
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetByBuyerVatIdAsync(string vatId)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .Include(i => i.Payee)
            .Where(i => i.Buyer.VatIdentifier == vatId)
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .Where(i => i.IssueDate >= startDate && i.IssueDate <= endDate)
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync();
    }

    public async Task<Party?> GetPartyByVatIdAsync(string vatId)
    {
        return await _context.Parties
            .FirstOrDefaultAsync(p => p.VatIdentifier == vatId);
    }

    public async Task<Party?> GetPartyByLegalIdAsync(string legalId)
    {
        return await _context.Parties
            .FirstOrDefaultAsync(p => p.LegalRegistrationId == legalId);
    }

    public async Task<List<Invoice>> GetRecentInvoicesAsync(int count = 50)
    {
        return await _context.Invoices
            .Include(i => i.Seller)
            .Include(i => i.Buyer)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Invoice> AddAsync(Invoice invoice)
    {
        try
        {
            _logger.LogInformation("Adding invoice {InvoiceNumber} to database", invoice.Number);

            // Handle party relationships
            await HandlePartyRelationships(invoice);

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully added invoice {InvoiceNumber} with ID {InvoiceId}", 
                invoice.Number, invoice.Id);

            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding invoice {InvoiceNumber}", invoice.Number);
            throw;
        }
    }

    public async Task<Invoice> UpdateAsync(Invoice invoice)
    {
        try
        {
            _logger.LogInformation("Updating invoice {InvoiceNumber}", invoice.Number);

            _context.Entry(invoice).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated invoice {InvoiceNumber}", invoice.Number);

            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating invoice {InvoiceNumber}", invoice.Number);
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        try
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _logger.LogInformation("Deleting invoice {InvoiceNumber}", invoice.Number);

                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted invoice {InvoiceNumber}", invoice.Number);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting invoice with ID {InvoiceId}", id);
            throw;
        }
    }

    public async Task<int> GetInvoiceCountAsync()
    {
        return await _context.Invoices.CountAsync();
    }

    public async Task<decimal> GetTotalAmountAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var query = _context.Invoices.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(i => i.IssueDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(i => i.IssueDate <= toDate.Value);

        return await query.SumAsync(i => i.TotalWithVat);
    }

    public async Task<bool> ExistsAsync(string invoiceNumber)
    {
        return await _context.Invoices
            .AnyAsync(i => i.Number == invoiceNumber);
    }

    private async Task HandlePartyRelationships(Invoice invoice)
    {
        // Handle seller
        if (invoice.Seller != null)
        {
            var existingSeller = await GetExistingPartyAsync(invoice.Seller);
            if (existingSeller != null)
            {
                invoice.Seller = existingSeller;
                invoice.SellerId = existingSeller.Id;
            }
            else
            {
                _context.Parties.Add(invoice.Seller);
            }
        }

        // Handle buyer
        if (invoice.Buyer != null)
        {
            var existingBuyer = await GetExistingPartyAsync(invoice.Buyer);
            if (existingBuyer != null)
            {
                invoice.Buyer = existingBuyer;
                invoice.BuyerId = existingBuyer.Id;
            }
            else
            {
                _context.Parties.Add(invoice.Buyer);
            }
        }

        // Handle payee
        if (invoice.Payee != null)
        {
            var existingPayee = await GetExistingPartyAsync(invoice.Payee);
            if (existingPayee != null)
            {
                invoice.Payee = existingPayee;
                invoice.PayeeId = existingPayee.Id;
            }
            else
            {
                _context.Parties.Add(invoice.Payee);
            }
        }
    }

    private async Task<Party?> GetExistingPartyAsync(Party party)
    {
        // Try to find by VAT ID first
        if (!string.IsNullOrEmpty(party.VatIdentifier))
        {
            var existingParty = await _context.Parties
                .FirstOrDefaultAsync(p => p.VatIdentifier == party.VatIdentifier);
            if (existingParty != null)
                return existingParty;
        }

        // Then try legal registration ID
        if (!string.IsNullOrEmpty(party.LegalRegistrationId))
        {
            var existingParty = await _context.Parties
                .FirstOrDefaultAsync(p => p.LegalRegistrationId == party.LegalRegistrationId);
            if (existingParty != null)
                return existingParty;
        }

        // Finally try by name (less reliable)
        var partyByName = await _context.Parties
            .FirstOrDefaultAsync(p => p.Name == party.Name);
        
        return partyByName;
    }
}