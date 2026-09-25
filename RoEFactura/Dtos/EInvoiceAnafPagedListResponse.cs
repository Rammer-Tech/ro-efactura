using System.Text.Json.Serialization;

namespace RoEFactura.Dtos;

public class EInvoiceAnafPagedListResponse
{
    [JsonPropertyName("mesaje")]
    public List<EInvoiceAnafResponse> Items { get; set; } = [];

    [JsonPropertyName("numar_inregistrari_in_pagina")]
    public int CurrentPageCount { get; set; }

    [JsonPropertyName("numar_total_inregistrari_per_pagina")]
    public int MaxPageCount { get; set; }

    [JsonPropertyName("numar_total_inregistrari")]
    public int TotalItemCount { get; set; }

    [JsonPropertyName("numar_total_pagini")]
    public int PageCount { get; set; }

    [JsonPropertyName("index_pagina_curenta")]
    public int CurrentPageIndex { get; set; }

    [JsonPropertyName("serial")]
    public string Serial { get; set; }

    [JsonPropertyName("cui")]
    public string Cui { get; set; }

    [JsonPropertyName("titlu")]
    public string Title { get; set; }

    /// <summary>
    /// ANAF's error message ("eroare") when the request could not be fulfilled (e.g. no messages in
    /// the requested interval). When set, <see cref="Items"/> is empty.
    /// </summary>
    [JsonPropertyName("eroare")]
    public string? Error { get; set; }
}
