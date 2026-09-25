using System.Text.Json.Serialization;

namespace RoEFactura.Models;

public class Token
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string TokenType { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; }
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }

    /// <summary>
    /// UTC instant this token was issued (set by the client when it parses the token response). Not part of the ANAF wire format.
    /// </summary>
    [JsonIgnore] public DateTimeOffset? IssuedAtUtc { get; set; }

    /// <summary>
    /// The instant <see cref="AccessToken"/> expires, computed from <see cref="IssuedAtUtc"/> and <see cref="ExpiresIn"/>.
    /// </summary>
    [JsonIgnore] public DateTimeOffset? ExpiresAtUtc => IssuedAtUtc?.AddSeconds(ExpiresIn);

    /// <summary>
    /// The instant <see cref="RefreshToken"/> expires, computed from <see cref="IssuedAtUtc"/> and <see cref="RefreshTokenLifetime"/>. Null when there is no refresh token.
    /// </summary>
    [JsonIgnore] public DateTimeOffset? RefreshTokenExpiresAtUtc => string.IsNullOrEmpty(RefreshToken) ? null : IssuedAtUtc?.Add(RefreshTokenLifetime);

    /// <summary>
    /// ANAF refresh-token lifetime policy as of 2026 (OAuth procedure PDF 2024-10-17). ANAF may change this without notice.
    /// </summary>
    public static TimeSpan RefreshTokenLifetime { get; } = TimeSpan.FromDays(365);
}
