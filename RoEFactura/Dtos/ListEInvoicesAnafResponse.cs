using System.Text.Json.Serialization;

namespace RoEFactura.Dtos;

public class ListEInvoicesAnafResponse
{
    [JsonPropertyName("mesaje")]
    public List<EInvoiceAnafResponse> Items { get; set; } = [];

    /// <summary>
    /// ANAF's error message ("eroare") when the request could not be fulfilled (e.g. no messages in
    /// the requested interval). When set, <see cref="Items"/> is empty.
    /// </summary>
    [JsonPropertyName("eroare")]
    public string? Error { get; set; }

    [JsonPropertyName("titlu")]
    public string? Title { get; set; }
}
