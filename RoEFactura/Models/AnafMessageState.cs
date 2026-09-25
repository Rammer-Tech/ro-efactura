namespace RoEFactura.Models;

/// <summary>
/// The processing state of a previously uploaded message, as returned by <c>stareMesaj</c>.
/// </summary>
public enum AnafMessageState
{
    /// <summary>ANAF is still processing the upload ("in prelucrare").</summary>
    InProcessing,

    /// <summary>The invoice was validated and processed ("ok").</summary>
    Ok,

    /// <summary>Errors were found; the file was not processed ("nok").</summary>
    Nok,

    /// <summary>The file was rejected at upload time ("XML cu erori nepreluat de sistem").</summary>
    RejectedAtUpload,

    /// <summary>ANAF returned a <c>stare</c> value this client does not recognise, or none at all.</summary>
    Unknown
}
