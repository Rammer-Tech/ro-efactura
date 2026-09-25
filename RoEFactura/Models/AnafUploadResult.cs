namespace RoEFactura.Models;

/// <summary>
/// Parsed response from ANAF's upload/uploadb2c endpoint.
/// </summary>
public sealed class AnafUploadResult
{
    public bool IsSuccess { get; init; }
    public string? UploadIndex { get; init; }
    public DateTimeOffset? ResponseDate { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public required string RawResponse { get; init; }
}
