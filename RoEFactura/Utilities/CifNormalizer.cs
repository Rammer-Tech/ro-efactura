namespace RoEFactura.Utilities;

/// <summary>
/// Normalizes a Romanian CIF/CUI to the plain digit string ANAF expects on its e-Factura query
/// parameters (no <c>RO</c> prefix, no whitespace).
/// </summary>
public static class CifNormalizer
{
    /// <summary>
    /// Trims the value, strips a leading <c>RO</c> prefix (case-insensitive) and removes all whitespace.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="cif"/> is null/blank, or the normalized value is not all ASCII digits.
    /// </exception>
    public static string Normalize(string cif)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cif);

        string value = cif.Trim();
        if (value.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        value = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if (value.Length == 0 || !value.All(c => c is >= '0' and <= '9'))
        {
            throw new ArgumentException(
                "CIF must contain only digits (optional RO prefix).", nameof(cif));
        }

        return value;
    }
}
