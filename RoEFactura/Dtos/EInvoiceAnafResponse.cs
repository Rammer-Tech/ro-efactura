using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RoEFactura.Dtos;

public class EInvoiceAnafResponse
{
    [JsonProperty("data_creare")]
    [JsonPropertyName("data_creare")]
    public string CreatedAt { get; set; }

    [JsonProperty("cif")]
    [JsonPropertyName("cif")]
    public string Cif { get; set; }

    [JsonProperty("id_solicitare")]
    [JsonPropertyName("id_solicitare")]
    public string RequestId { get; set; }

    [JsonProperty("detalii")]
    [JsonPropertyName("detalii")]
    public string Details { get; set; }

    [JsonProperty("tip")]
    [JsonPropertyName("tip")]
    public string Type { get; set; }

    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// CIF-ul emitentului facturii (seller/supplier).
    /// </summary>
    [JsonProperty("cif_emitent")]
    [JsonPropertyName("cif_emitent")]
    public string? SellerCif { get; set; }

    /// <summary>
    /// CIF-ul beneficiarului facturii (buyer/customer).
    /// </summary>
    [JsonProperty("cif_beneficiar")]
    [JsonPropertyName("cif_beneficiar")]
    public string? BuyerCif { get; set; }
}