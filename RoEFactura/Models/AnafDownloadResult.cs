namespace RoEFactura.Models;

/// <summary>
/// The result of downloading and splitting an ANAF <c>descarcare</c> ZIP entirely in memory.
/// </summary>
public sealed class AnafDownloadResult
{
    /// <summary>The raw ZIP bytes as returned by ANAF.</summary>
    public required byte[] ZipContent { get; init; }

    /// <summary>The file name of the downloaded ZIP (from Content-Disposition, or a generated fallback).</summary>
    public required string FileName { get; init; }

    /// <summary>The invoice/error XML entry, or null if the ZIP contained only a signature sidecar.</summary>
    public byte[]? DocumentXml { get; init; }

    /// <summary>The archive entry name of <see cref="DocumentXml"/>.</summary>
    public string? DocumentFileName { get; init; }

    /// <summary>The Ministry of Finance signature XML entry, if present.</summary>
    public byte[]? SignatureXml { get; init; }

    /// <summary>The archive entry name of <see cref="SignatureXml"/>.</summary>
    public string? SignatureFileName { get; init; }
}
