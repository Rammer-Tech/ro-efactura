using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using RoEFactura.Models;

namespace RoEFactura.Services.Api;

/// <summary>
/// Pure parsing of ANAF e-Factura response bodies. XML parsing matches element/attribute
/// <see cref="XName.LocalName"/> and ignores namespaces, because ANAF uses a different XML
/// namespace per endpoint.
/// </summary>
internal static class AnafResponseParser
{
    /// <summary>Parses the <c>header</c> XML returned by upload/uploadb2c.</summary>
    /// <exception cref="AnafApiException">The body is not well-formed XML with a root element.</exception>
    public static AnafUploadResult ParseUpload(string raw)
    {
        XElement root = ParseXmlRoot(raw, "Unexpected ANAF upload response.");

        string? executionStatus = (string?)root.Attribute("ExecutionStatus");
        string? indexIncarcare = (string?)root.Attribute("index_incarcare");
        DateTimeOffset? responseDate = ParseAnafDate((string?)root.Attribute("dateResponse"));

        List<string> errors = ReadErrorMessages(root);

        bool hasIndex = !string.IsNullOrWhiteSpace(indexIncarcare);
        bool isSuccess;

        if (executionStatus == "0")
        {
            if (hasIndex)
            {
                isSuccess = true;
            }
            else
            {
                isSuccess = false;
                errors = ["ANAF upload response has no index_incarcare."];
            }
        }
        else
        {
            isSuccess = false;
        }

        return new AnafUploadResult
        {
            IsSuccess = isSuccess,
            UploadIndex = hasIndex ? indexIncarcare : null,
            ResponseDate = responseDate,
            Errors = errors,
            RawResponse = raw
        };
    }

    /// <summary>Parses the <c>header</c> XML returned by <c>stareMesaj</c>.</summary>
    /// <exception cref="AnafApiException">The body is not well-formed XML with a root element.</exception>
    public static AnafMessageStatusResult ParseMessageStatus(string raw)
    {
        XElement root = ParseXmlRoot(raw, "Unexpected ANAF message-status response.");

        string? stare = (string?)root.Attribute("stare");
        string? downloadId = (string?)root.Attribute("id_descarcare");
        List<string> errors = ReadErrorMessages(root);

        AnafMessageState state = stare switch
        {
            "ok" => AnafMessageState.Ok,
            "nok" => AnafMessageState.Nok,
            "in prelucrare" => AnafMessageState.InProcessing,
            "XML cu erori nepreluat de sistem" => AnafMessageState.RejectedAtUpload,
            _ => AnafMessageState.Unknown
        };

        return new AnafMessageStatusResult
        {
            State = state,
            DownloadId = string.IsNullOrWhiteSpace(downloadId) ? null : downloadId,
            Errors = errors,
            RawResponse = raw
        };
    }

    /// <summary>
    /// Attempts to parse the <c>{"eroare": "...", "titlu": "..."}</c> shape ANAF returns for the
    /// list, download and paged-list endpoints instead of a normal payload.
    /// </summary>
    public static bool TryParseEroare(string raw, out string? eroare, out string? titlu)
    {
        eroare = null;
        titlu = null;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!document.RootElement.TryGetProperty("eroare", out JsonElement eroareElement)
                || eroareElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            eroare = eroareElement.GetString();

            if (document.RootElement.TryGetProperty("titlu", out JsonElement titluElement)
                && titluElement.ValueKind == JsonValueKind.String)
            {
                titlu = titluElement.GetString();
            }

            return !string.IsNullOrWhiteSpace(eroare);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Parses the JSON response returned by the public <c>validare</c> endpoint.</summary>
    /// <exception cref="AnafApiException">The body is not JSON, or has no <c>stare</c> field.</exception>
    public static AnafValidationResult ParseValidation(string raw)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new AnafApiException(HttpStatusCode.OK, "Unexpected ANAF validation response.", raw);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("stare", out JsonElement stareElement)
                || stareElement.ValueKind != JsonValueKind.String)
            {
                throw new AnafApiException(HttpStatusCode.OK, "Unexpected ANAF validation response.", raw);
            }

            string? stare = stareElement.GetString();
            List<string> messages = [];

            if (document.RootElement.TryGetProperty("Messages", out JsonElement messagesElement)
                && messagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in messagesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty("message", out JsonElement messageElement)
                        && messageElement.ValueKind == JsonValueKind.String)
                    {
                        string? message = messageElement.GetString();
                        if (!string.IsNullOrEmpty(message))
                            messages.Add(message);
                    }
                }
            }

            string? traceId = document.RootElement.TryGetProperty("trace_id", out JsonElement traceIdElement)
                               && traceIdElement.ValueKind == JsonValueKind.String
                ? traceIdElement.GetString()
                : null;

            return new AnafValidationResult
            {
                IsValid = stare == "ok",
                Messages = messages,
                TraceId = traceId,
                RawResponse = raw
            };
        }
    }

    private static XElement ParseXmlRoot(string raw, string errorMessage)
    {
        try
        {
            return XDocument.Parse(raw).Root ?? throw new XmlException("No root element.");
        }
        catch (XmlException)
        {
            throw new AnafApiException(HttpStatusCode.OK, errorMessage, raw);
        }
    }

    private static List<string> ReadErrorMessages(XElement root)
    {
        return root.Elements()
            .Where(e => e.Name.LocalName == "Errors")
            .Select(e => (string?)e.Attribute("errorMessage"))
            .Where(m => !string.IsNullOrEmpty(m))
            .Select(m => m!)
            .ToList();
    }

    /// <summary>
    /// Parses an ANAF <c>dateResponse</c> value (<c>yyyyMMddHHmm</c>, invariant culture) as a wall-clock
    /// time in the Europe/Bucharest time zone. Returns null when the value is absent, malformed, or the
    /// time zone cannot be resolved on the current platform.
    /// </summary>
    private static DateTimeOffset? ParseAnafDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!DateTime.TryParseExact(
                raw, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local))
        {
            return null;
        }

        try
        {
            TimeZoneInfo bucharest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
            TimeSpan offset = bucharest.GetUtcOffset(local);
            return new DateTimeOffset(local, offset);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
