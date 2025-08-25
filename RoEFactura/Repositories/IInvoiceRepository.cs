using RoEFactura.Domain.Entities;

namespace RoEFactura.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id);
    Task<Invoice?> GetByNumberAsync(string invoiceNumber);
    Task<Invoice?> GetByAnafDownloadIdAsync(string anafDownloadId);
    Task<List<Invoice>> GetBySellerVatIdAsync(string vatId);
    Task<List<Invoice>> GetByBuyerVatIdAsync(string vatId);
    Task<List<Invoice>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate);
    Task<Party?> GetPartyByVatIdAsync(string vatId);
    Task<Party?> GetPartyByLegalIdAsync(string legalId);
    Task<List<Invoice>> GetRecentInvoicesAsync(int count = 50);
    Task<Invoice> AddAsync(Invoice invoice);
    Task<Invoice> UpdateAsync(Invoice invoice);
    Task DeleteAsync(Guid id);
    Task<int> GetInvoiceCountAsync();
    Task<decimal> GetTotalAmountAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
    Task<bool> ExistsAsync(string invoiceNumber);
}