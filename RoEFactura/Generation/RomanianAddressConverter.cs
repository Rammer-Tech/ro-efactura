using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RoEFactura.Validation.Constants;

namespace RoEFactura.Generation;

/// <summary>
/// A Romanian address converted to the CIUS-RO form.
/// </summary>
/// <param name="CountySubentity">ISO 3166-2:RO county code for BT-39/BT-54, e.g. <c>RO-CJ</c> or <c>RO-B</c>.</param>
/// <param name="CityName">City for BT-37/BT-52: the trimmed input, or <c>SECTOR1</c>..<c>SECTOR6</c> for Bucharest.</param>
public sealed record RomanianAddress(string CountySubentity, string CityName);

/// <summary>
/// Converts Romanian county and Bucharest sector names to the codes CIUS-RO requires: the county becomes
/// its ISO 3166-2:RO code (BR-RO-110/BR-RO-111) and a Bucharest city becomes <c>SECTOR1</c>..<c>SECTOR6</c>
/// (BR-RO-100/BR-RO-101).
/// </summary>
/// <remarks>
/// Matching ignores case, diacritics (both the comma-below ș/ț and the cedilla ş/ţ forms), hyphens versus
/// spaces and repeated whitespace, and strips the prefixes <c>județul</c>, <c>judet</c>, <c>jud.</c>,
/// <c>jud</c>, <c>municipiul</c> and <c>mun.</c>. A county may be given by its full name (<c>Cluj</c>),
/// its abbreviation (<c>CJ</c>) or its ISO code (<c>RO-CJ</c>). The valid codes are
/// <see cref="RomanianConstants.ValidCountyCodes"/>.
/// </remarks>
public static class RomanianAddressConverter
{
    /// <summary>Normalized (lower-case, no diacritics, hyphen as space) county name per ISO 3166-2:RO code.</summary>
    private static readonly Dictionary<string, string> CountyNameByCode = new(StringComparer.Ordinal)
    {
        ["RO-AB"] = "alba",
        ["RO-AR"] = "arad",
        ["RO-AG"] = "arges",
        ["RO-B"] = "bucuresti",
        ["RO-BC"] = "bacau",
        ["RO-BH"] = "bihor",
        ["RO-BN"] = "bistrita nasaud",
        ["RO-BT"] = "botosani",
        ["RO-BV"] = "brasov",
        ["RO-BR"] = "braila",
        ["RO-BZ"] = "buzau",
        ["RO-CS"] = "caras severin",
        ["RO-CL"] = "calarasi",
        ["RO-CJ"] = "cluj",
        ["RO-CT"] = "constanta",
        ["RO-CV"] = "covasna",
        ["RO-DB"] = "dambovita",
        ["RO-DJ"] = "dolj",
        ["RO-GL"] = "galati",
        ["RO-GR"] = "giurgiu",
        ["RO-GJ"] = "gorj",
        ["RO-HR"] = "harghita",
        ["RO-HD"] = "hunedoara",
        ["RO-IL"] = "ialomita",
        ["RO-IS"] = "iasi",
        ["RO-IF"] = "ilfov",
        ["RO-MM"] = "maramures",
        ["RO-MH"] = "mehedinti",
        ["RO-MS"] = "mures",
        ["RO-NT"] = "neamt",
        ["RO-OT"] = "olt",
        ["RO-PH"] = "prahova",
        ["RO-SM"] = "satu mare",
        ["RO-SJ"] = "salaj",
        ["RO-SB"] = "sibiu",
        ["RO-SV"] = "suceava",
        ["RO-TR"] = "teleorman",
        ["RO-TM"] = "timis",
        ["RO-TL"] = "tulcea",
        ["RO-VS"] = "vaslui",
        ["RO-VL"] = "valcea",
        ["RO-VN"] = "vrancea"
    };

    /// <summary>The codes come from <see cref="RomanianConstants.ValidCountyCodes"/> only (BR-RO-110/111 list).</summary>
    private static readonly HashSet<string> ValidCountyCodes =
        new(RomanianConstants.ValidCountyCodes, StringComparer.Ordinal);

    private static readonly Dictionary<string, string> CountyCodeByName = RomanianConstants.ValidCountyCodes
        .Where(CountyNameByCode.ContainsKey)
        .ToDictionary(code => CountyNameByCode[code], code => code, StringComparer.Ordinal);

    private static readonly string[] CountyPrefixes =
        ["judetul ", "judet ", "jud. ", "jud ", "municipiul ", "mun. "];

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.CultureInvariant);

    private static readonly Regex AbbreviationPattern = new(@"^(?:ro )?([a-z]{1,2})$", RegexOptions.CultureInvariant);

    /// <summary>Whole-value sector forms: <c>sector 3</c>, <c>sectorul 3</c>, <c>sector3</c>, <c>s3</c>, <c>3</c>.</summary>
    private static readonly Regex ExactSectorPattern =
        new(@"^(?:sectorul|sector|s)?\s*([0-9]+)$", RegexOptions.CultureInvariant);

    /// <summary>A sector named inside a longer city value, e.g. <c>București, Sector 3</c>.</summary>
    private static readonly Regex EmbeddedSectorPattern =
        new(@"(?:^|[^a-z0-9])sector(?:ul)?\s*([1-6])(?![0-9])", RegexOptions.CultureInvariant);

    /// <summary>
    /// Converts a county name, abbreviation or ISO code to its ISO 3166-2:RO code (BR-RO-110/BR-RO-111).
    /// </summary>
    /// <param name="county">E.g. <c>Brașov</c>, <c>județul Brașov</c>, <c>BV</c>, <c>RO-BV</c>, <c>București</c>.</param>
    /// <returns>The code, e.g. <c>RO-BV</c> or <c>RO-B</c>.</returns>
    /// <exception cref="ArgumentException">The county is blank or unknown (message starts with <c>[BR-RO-110]</c>).</exception>
    public static string ToCountyCode(string county)
    {
        if (TryToCountyCode(county, out string code))
        {
            return code;
        }

        // BR-RO-110/BR-RO-111: a Romanian address must carry a county from the ISO 3166-2:RO list.
        if (string.IsNullOrWhiteSpace(county))
        {
            throw new ArgumentException("[BR-RO-110] A Romanian county is required.", nameof(county));
        }

        throw new ArgumentException($"[BR-RO-110] Unknown Romanian county '{county}'.", nameof(county));
    }

    /// <summary>
    /// Converts a county name, abbreviation or ISO code to its ISO 3166-2:RO code without throwing.
    /// </summary>
    /// <param name="county">The county as entered by a user; may be null.</param>
    /// <param name="code">The ISO 3166-2:RO code, or an empty string when the county is not recognized.</param>
    /// <returns><c>true</c> when the county was recognized.</returns>
    public static bool TryToCountyCode(string? county, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(county))
        {
            return false;
        }

        if (Normalize(county) is not { } normalized)
        {
            return false;
        }

        string key = StripCountyPrefix(normalized);

        if (CountyCodeByName.TryGetValue(key, out string? byName))
        {
            code = byName;
            return true;
        }

        // Abbreviation ("cj", "b") or ISO code ("ro-cj", already hyphen-normalized to "ro cj").
        Match match = AbbreviationPattern.Match(key);
        if (match.Success)
        {
            string candidate = "RO-" + match.Groups[1].Value.ToUpperInvariant();
            if (ValidCountyCodes.Contains(candidate))
            {
                code = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a Bucharest city value to its SECTOR-RO code (BR-RO-100/BR-RO-101).
    /// </summary>
    /// <param name="city">
    /// E.g. <c>Sector 3</c>, <c>sectorul 3</c>, <c>S3</c>, <c>3</c>, <c>SECTOR3</c> or <c>București, Sector 3</c>.
    /// </param>
    /// <returns><c>SECTOR1</c>..<c>SECTOR6</c>.</returns>
    /// <exception cref="ArgumentException">
    /// The value names no sector or a sector outside 1-6 (message starts with <c>[BR-RO-100]</c>).
    /// </exception>
    public static string ToBucharestSector(string city)
    {
        if (TryToBucharestSector(city, out string sector))
        {
            return sector;
        }

        throw new ArgumentException(
            $"[BR-RO-100] '{city}' does not name a Bucharest sector; expected SECTOR1..SECTOR6 (e.g. 'Sector 3').",
            nameof(city));
    }

    /// <summary>
    /// Converts a county and city to the CIUS-RO form: the county becomes its ISO 3166-2:RO code and, for
    /// Bucharest (<c>RO-B</c>), the city becomes <c>SECTOR1</c>..<c>SECTOR6</c> (BR-RO-100/101/110/111).
    /// Other cities are kept as given (trimmed).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The county is unknown (<c>[BR-RO-110]</c>), the city is blank (<c>[BR-RO-091]</c>), or a Bucharest
    /// city carries no sector 1-6 (<c>[BR-RO-100]</c>).
    /// </exception>
    public static RomanianAddress Convert(string county, string city)
    {
        string countyCode = ToCountyCode(county);

        // BR-RO-091/092: the city is mandatory.
        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("[BR-RO-091] The city must not be blank.", nameof(city));
        }

        if (countyCode == RomanianConstants.BucharestCountyCode)
        {
            // BR-RO-100/101: country RO and county RO-B => city coded with the SECTOR-RO list.
            if (!TryToBucharestSector(city, out string sector))
            {
                throw new ArgumentException(
                    $"[BR-RO-100] Bucharest address requires a sector (1-6) in the city; got '{city}'.",
                    nameof(city));
            }

            return new RomanianAddress(countyCode, sector);
        }

        return new RomanianAddress(countyCode, city.Trim());
    }

    /// <summary>Parses a SECTOR-RO code (1..6) from a city value; used by the generator guard as well.</summary>
    internal static bool TryToBucharestSector(string? city, out string sector)
    {
        sector = string.Empty;
        if (string.IsNullOrWhiteSpace(city))
        {
            return false;
        }

        if (Normalize(city) is not { } key)
        {
            return false;
        }

        Match exact = ExactSectorPattern.Match(key);
        Match match = exact.Success ? exact : EmbeddedSectorPattern.Match(key);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            || number < 1
            || number > RomanianConstants.BucharestSectorCodes.Count)
        {
            return false;
        }

        sector = RomanianConstants.BucharestSectorCodes[number - 1];
        return true;
    }

    /// <summary>
    /// Trim, NFD then drop non-spacing marks (ș/ş/ț/ţ/ă/â/î), lower-case (invariant), dashes as spaces,
    /// whitespace runs collapsed to one space. Returns <c>null</c> when the value is not valid Unicode
    /// (e.g. a lone surrogate), which names no county or sector.
    /// </summary>
    private static string? Normalize(string value)
    {
        string decomposed;
        try
        {
            decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            // string.Normalize rejects invalid code points: lone surrogates and, on ICU, U+FFFE.
            return null;
        }

        StringBuilder builder = new(decomposed.Length);
        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(category == UnicodeCategory.DashPunctuation ? ' ' : char.ToLowerInvariant(c));
        }

        return WhitespaceRun.Replace(builder.ToString(), " ").Trim();
    }

    private static string StripCountyPrefix(string key)
    {
        foreach (string prefix in CountyPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length)
            {
                return key[prefix.Length..].Trim();
            }
        }

        return key;
    }
}
