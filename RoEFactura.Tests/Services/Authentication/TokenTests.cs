using System.Text.Json;
using FluentAssertions;
using RoEFactura.Models;
using Xunit;

namespace RoEFactura.Tests.Services.Authentication;

public class TokenTests
{
    private static Token CreateToken(DateTimeOffset issuedAt) => new()
    {
        AccessToken = "access-value",
        TokenType = "Bearer",
        Scope = "e-factura",
        RefreshToken = "refresh-value",
        ExpiresIn = 5184000,
        IssuedAtUtc = issuedAt,
    };

    [Fact]
    public void ExpiresAtUtc_IsIssuedAtPlusExpiresIn()
    {
        var issuedAt = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
        Token token = CreateToken(issuedAt);

        token.ExpiresAtUtc.Should().Be(issuedAt.AddSeconds(token.ExpiresIn));
    }

    [Fact]
    public void RefreshTokenExpiresAtUtc_IsIssuedAtPlus365Days()
    {
        var issuedAt = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
        Token token = CreateToken(issuedAt);

        Token.RefreshTokenLifetime.Should().Be(TimeSpan.FromDays(365));
        token.RefreshTokenExpiresAtUtc.Should().Be(issuedAt.AddDays(365));
    }

    [Fact]
    public void Serialize_KeepsSnakeCaseNamesAndOmitsComputedProperties()
    {
        Token token = CreateToken(DateTimeOffset.UtcNow);

        string json = JsonSerializer.Serialize(token);

        json.Should().Contain("\"access_token\":\"access-value\"");
        json.Should().Contain("\"refresh_token\":\"refresh-value\"");
        json.Should().Contain("\"expires_in\":5184000");
        json.Should().Contain("\"token_type\":\"Bearer\"");
        json.Should().Contain("\"scope\":\"e-factura\"");
        json.Should().NotContain("IssuedAtUtc");
        json.Should().NotContain("ExpiresAtUtc");
        json.Should().NotContain("RefreshTokenExpiresAtUtc");
        json.Should().NotContain("RefreshTokenLifetime");
    }
}
