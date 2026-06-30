namespace RoEFactura.Models;

/// <summary>
/// ANAF returned a JSON error indicating the invoice download window (typically 60 days) has expired.
/// </summary>
public class AnafDownloadWindowExpiredException : Exception
{
    public string AnafDownloadId { get; }
    public string? AnafErrorMessage { get; }

    public AnafDownloadWindowExpiredException(
        string anafDownloadId,
        string? anafErrorMessage = null,
        Exception? innerException = null)
        : base(
            $"ANAF download {anafDownloadId} is no longer available (download window expired).",
            innerException)
    {
        AnafDownloadId = anafDownloadId;
        AnafErrorMessage = anafErrorMessage;
    }
}
