using System.Text.Json;

namespace RoEFactura.Services.Api;

/// <summary>
/// Parses ANAF download error JSON bodies returned with HTTP 200 and no Content-Disposition.
/// </summary>
public static class AnafDownloadErrorParser
{
    /// <summary>
    /// Returns true when the response body indicates the invoice can no longer be downloaded
    /// because the ANAF availability window (typically 60 days) has passed.
    /// </summary>
    public static bool TryGetDownloadWindowExpiredMessage(string body, out string? anafErrorMessage)
    {
        anafErrorMessage = null;

        if (string.IsNullOrWhiteSpace(body))
            return false;

        if (TryParseEroareField(body, out anafErrorMessage)
            && IsDownloadWindowExpiredMessage(anafErrorMessage))
        {
            return true;
        }

        if (IsDownloadWindowExpiredMessage(body))
        {
            anafErrorMessage = body.Trim();
            return true;
        }

        return false;
    }

    public static bool IsDownloadWindowExpiredMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var normalized = NormalizeForMatch(message);
        return normalized.Contains("nu mai poate fi descarcat", StringComparison.Ordinal)
               && normalized.Contains("60 de zile", StringComparison.Ordinal);
    }

    private static bool TryParseEroareField(string body, out string? eroare)
    {
        eroare = null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("eroare", out JsonElement errorElement)
                && errorElement.ValueKind == JsonValueKind.String)
            {
                eroare = errorElement.GetString();
                return !string.IsNullOrWhiteSpace(eroare);
            }
        }
        catch (JsonException)
        {
            // Fall back to raw body matching below.
        }

        return false;
    }

    private static string NormalizeForMatch(string value) =>
        value.Trim().ToLowerInvariant()
            .Replace('ă', 'a')
            .Replace('â', 'a')
            .Replace('î', 'i')
            .Replace('ș', 's')
            .Replace('ş', 's')
            .Replace('ț', 't')
            .Replace('ţ', 't');
}
