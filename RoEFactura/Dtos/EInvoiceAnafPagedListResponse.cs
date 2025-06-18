using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RoEFactura.Dtos;

public class EInvoiceAnafPagedListResponse
{
    [JsonProperty("mesaje")]
    [JsonPropertyName("mesaje")]
    public List<EInvoiceAnafResponse> Items { get; set; }

    [JsonProperty("numar_inregistrari_in_pagina")]
    [JsonPropertyName("numar_inregistrari_in_pagina")]
    public int CurrentPageCount { get; set; }

    [JsonProperty("numar_total_inregistrari_per_pagina")]
    [JsonPropertyName("numar_total_inregistrari_per_pagina")]
    public int MaxPageCount { get; set; }

    [JsonProperty("numar_total_inregistrari")]
    [JsonPropertyName("numar_total_inregistrari")]
    public int TotalItemCount { get; set; }

    [JsonProperty("numar_total_pagini")]
    [JsonPropertyName("numar_total_pagini")]
    public int PageCount { get; set; }

    [JsonProperty("index_pagina_curenta")]
    [JsonPropertyName("index_pagina_curenta")]
    public int CurrentPageIndex { get; set; }

    [JsonProperty("serial")]
    [JsonPropertyName("serial")]
    public string Serial { get; set; }

    [JsonProperty("cui")]
    [JsonPropertyName("cui")]
    public string Cui { get; set; }

    [JsonProperty("titlu")]
    [JsonPropertyName("titlu")]
    public string Title { get; set; }
}