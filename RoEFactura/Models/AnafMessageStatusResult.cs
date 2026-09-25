namespace RoEFactura.Models;

/// <summary>
/// Parsed response from ANAF's <c>stareMesaj</c> endpoint.
/// </summary>
public sealed class AnafMessageStatusResult
{
    public AnafMessageState State { get; init; }
    public string? DownloadId { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public required string RawResponse { get; init; }
}
