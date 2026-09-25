using System.Text.Json.Serialization;

namespace RoEFactura.Dtos;

public class EInvoiceAnafResponse
{
    [JsonPropertyName("data_creare")]
    public string CreatedAt { get; set; }

    [JsonPropertyName("cif")]
    public string Cif { get; set; }

    [JsonPropertyName("id_solicitare")]
    public string RequestId { get; set; }

    [JsonPropertyName("detalii")]
    public string Details { get; set; }

    [JsonPropertyName("tip")]
    public string Type { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// CIF-ul emitentului facturii (seller/supplier).
    /// </summary>
    [JsonPropertyName("cif_emitent")]
    public string? SellerCif { get; set; }

    /// <summary>
    /// CIF-ul beneficiarului facturii (buyer/customer).
    /// </summary>
    [JsonPropertyName("cif_beneficiar")]
    public string? BuyerCif { get; set; }
}
