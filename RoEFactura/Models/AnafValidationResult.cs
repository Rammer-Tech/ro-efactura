namespace RoEFactura.Models;

/// <summary>
/// Parsed response from ANAF's public <c>validare</c> endpoint.
/// </summary>
public sealed class AnafValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = [];
    public string? TraceId { get; init; }
    public required string RawResponse { get; init; }
}
