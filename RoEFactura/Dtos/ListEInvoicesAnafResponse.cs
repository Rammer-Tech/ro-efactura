using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RoEFactura.Dtos;

public class ListEInvoicesAnafResponse
{
    [JsonProperty("mesaje")]
    [JsonPropertyName("mesaje")]
    public List<EInvoiceAnafResponse> Items { get; set; }
}