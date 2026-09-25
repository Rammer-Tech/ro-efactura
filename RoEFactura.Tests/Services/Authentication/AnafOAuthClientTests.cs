using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;
using RoEFactura.Models;
using RoEFactura.Services.Authentication;
using Xunit;

namespace RoEFactura.Tests.Services.Authentication;

public class AnafOAuthClientTests
{
    private const string SuccessTokenJson =
        "{\"access_token\":\"access-abc\",\"refresh_token\":\"refresh-xyz\",\"expires_in\":5184000,\"token_type\":\"Bearer\",\"scope\":\"e-factura\"}";

    private static (AnafOAuthClient Client, OAuthRecordingHttpMessageHandler Handler, CapturingLogger<AnafOAuthClient> Logger) CreateClient(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string body = SuccessTokenJson,
        string mediaType = "application/json")
    {
        var handler = OAuthRecordingHttpMessageHandler.Returning(statusCode, body, mediaType);
        var logger = new CapturingLogger<AnafOAuthClient>();
        var client = new AnafOAuthClient(new FakeHttpClientFactory(handler), logger);
        return (client, handler, logger);
    }

    private static AnafOAuthOptions ValidOptions(string? tokenUrl = null, bool includeTokenContentType = true) => new()
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        RedirectUri = "https://app.example.test/callback",
        TokenUrl = tokenUrl ?? "https://oauth.example.test/token",
        IncludeTokenContentType = includeTokenContentType,
    };

    [Fact]
    public void GenerateAuthorizationUrl_Options_UsesOptionsAuthorizeUrlAndEscapesValues()
    {
        var (client, _, _) = CreateClient();
        var options = new AnafOAuthOptions
        {
            ClientId = "client id/1",
            ClientSecret = "secret",
            RedirectUri = "https://app.example.test/callback?x=1&y=2",
            AuthorizeUrl = "https://oauth.example.test/authorize",
            TokenUrl = "https://oauth.example.test/token",
            Prompt = null,
            Nonce = null,
        };

        string url = client.GenerateAuthorizationUrl(options, "st ate/2");

        url.Should().StartWith("https://oauth.example.test/authorize?");

        var uri = new Uri(url);
        var query = HttpUtility.ParseQueryString(uri.Query);
        query["response_type"].Should().Be("code");
        query["client_id"].Should().Be("client id/1");
        query["redirect_uri"].Should().Be("https://app.example.test/callback?x=1&y=2");
        query["state"].Should().Be("st ate/2");
        query["token_content_type"].Should().Be("jwt");
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_Options_PostsFormToOptionsTokenUrlWithBasicAuth()
    {
        var (client, handler, _) = CreateClient(body: SuccessTokenJson);
        AnafOAuthOptions options = ValidOptions(tokenUrl: "https://oauth.example.test/token");

        await client.ExchangeAuthorizationCodeAsync("auth-code-1", options);

        handler.LastRequestUri.Should().Be("https://oauth.example.test/token");
        handler.LastAuthorizationScheme.Should().Be("Basic");
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(handler.LastAuthorizationParameter!));
        decoded.Should().Be($"{options.ClientId}:{options.ClientSecret}");
        handler.LastContentMediaType.Should().Be("application/x-www-form-urlencoded");

        var form = HttpUtility.ParseQueryString(handler.LastRequestBody!);
        form["grant_type"].Should().Be("authorization_code");
        form["code"].Should().Be("auth-code-1");
        form["client_id"].Should().Be(options.ClientId);
        form["client_secret"].Should().Be(options.ClientSecret);
        form["redirect_uri"].Should().Be(options.RedirectUri);
        form["token_content_type"].Should().Be("jwt");
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_Options_ReturnsTokenWithIssuedAt()
    {
        var (client, _, _) = CreateClient(body: SuccessTokenJson);
        AnafOAuthOptions options = ValidOptions();

        DateTimeOffset before = DateTimeOffset.UtcNow;
        Token token = await client.ExchangeAuthorizationCodeAsync("auth-code-2", options);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        token.AccessToken.Should().Be("access-abc");
        token.RefreshToken.Should().Be("refresh-xyz");
        token.ExpiresIn.Should().Be(5184000);
        token.IssuedAtUtc.Should().NotBeNull();
        token.IssuedAtUtc!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_CancellationTokenOverload_ThrowsOperationCanceledWhenCancelled()
    {
        var handler = OAuthRecordingHttpMessageHandler.Hanging();
        var client = new AnafOAuthClient(new FakeHttpClientFactory(handler), new CapturingLogger<AnafOAuthClient>());
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Func<Task> act = () => client.ExchangeAuthorizationCodeAsync("auth-code", ValidOptions(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_FourStringOverload_UsesDefaultTokenUrl()
    {
        var (client, handler, _) = CreateClient(body: SuccessTokenJson);

        await client.ExchangeAuthorizationCodeAsync(
            "auth-code-3", "client-id", "client-secret", "https://app.example.test/callback");

        handler.LastRequestUri.Should().Be(AnafOAuthOptions.DefaultTokenUrl);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_PostsRefreshGrantToOptionsTokenUrlWithBasicAuth()
    {
        var (client, handler, _) = CreateClient(body: SuccessTokenJson);
        AnafOAuthOptions options = ValidOptions(tokenUrl: "https://oauth.example.test/token");

        await client.RefreshAccessTokenAsync("refresh-token-1", options);

        handler.LastRequestUri.Should().Be("https://oauth.example.test/token");
        handler.LastAuthorizationScheme.Should().Be("Basic");
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(handler.LastAuthorizationParameter!));
        decoded.Should().Be($"{options.ClientId}:{options.ClientSecret}");

        HashSet<string> formKeys = handler.LastRequestBody!
            .Split('&')
            .Select(pair => Uri.UnescapeDataString(pair.Split('=')[0]))
            .ToHashSet();
        formKeys.Should().BeEquivalentTo(["grant_type", "refresh_token", "token_content_type"]);

        var form = HttpUtility.ParseQueryString(handler.LastRequestBody!);
        form["grant_type"].Should().Be("refresh_token");
        form["refresh_token"].Should().Be("refresh-token-1");
        form["token_content_type"].Should().Be("jwt");
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_IncludeTokenContentTypeFalse_OmitsTokenContentType()
    {
        var (client, handler, _) = CreateClient(body: SuccessTokenJson);
        AnafOAuthOptions options = ValidOptions(includeTokenContentType: false);

        await client.RefreshAccessTokenAsync("refresh-token-2", options);

        handler.LastRequestBody.Should().NotContain("token_content_type");

        HashSet<string> formKeys = handler.LastRequestBody!
            .Split('&')
            .Select(pair => Uri.UnescapeDataString(pair.Split('=')[0]))
            .ToHashSet();
        formKeys.Should().BeEquivalentTo(["grant_type", "refresh_token"]);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_Success_ReturnsNewAccessAndRefreshTokens()
    {
        const string body =
            "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":5184000,\"token_type\":\"Bearer\"}";
        var (client, _, _) = CreateClient(body: body);
        AnafOAuthOptions options = ValidOptions();

        DateTimeOffset before = DateTimeOffset.UtcNow;
        Token token = await client.RefreshAccessTokenAsync("old-refresh-token", options);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        token.AccessToken.Should().Be("new-access");
        token.RefreshToken.Should().Be("new-refresh");
        token.ExpiresIn.Should().Be(5184000);
        token.IssuedAtUtc.Should().NotBeNull();
        token.IssuedAtUtc!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        token.RefreshTokenExpiresAtUtc.Should().Be(token.IssuedAtUtc!.Value.AddDays(365));
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_InvalidGrant_ThrowsTokenExchangeExceptionWithInvalidGrant()
    {
        const string body = "{\"error\":\"invalid_grant\",\"error_description\":\"Refresh token expired\"}";
        var (client, _, _) = CreateClient(HttpStatusCode.BadRequest, body);
        AnafOAuthOptions options = ValidOptions();

        Func<Task> act = () => client.RefreshAccessTokenAsync("expired-refresh-token", options);

        (await act.Should().ThrowAsync<TokenExchangeException>())
            .Which.ErrorType.Should().Be(TokenExchangeErrorType.InvalidGrant);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_Unauthorized_ThrowsAuthenticationFailed()
    {
        var (client, _, _) = CreateClient(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}");
        AnafOAuthOptions options = ValidOptions();

        Func<Task> act = () => client.RefreshAccessTokenAsync("some-refresh-token", options);

        (await act.Should().ThrowAsync<TokenExchangeException>())
            .Which.ErrorType.Should().Be(TokenExchangeErrorType.AuthenticationFailed);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_EmptyRefreshToken_ThrowsArgumentException()
    {
        var (client, _, _) = CreateClient();
        AnafOAuthOptions options = ValidOptions();

        Func<Task> act = () => client.RefreshAccessTokenAsync("", options);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_InvalidOptions_ThrowsArgumentException()
    {
        var (client, _, _) = CreateClient();
        var options = new AnafOAuthOptions();

        Func<Task> act = () => client.RefreshAccessTokenAsync("refresh-token", options);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TokenOperations_DoNotLogTokenValuesOrResponseBodies()
    {
        const string successBody =
            "{\"access_token\":\"super-secret-access\",\"refresh_token\":\"super-secret-refresh\",\"expires_in\":5184000,\"token_type\":\"Bearer\"}";
        var (successClient, _, successLogger) = CreateClient(body: successBody);

        await successClient.ExchangeAuthorizationCodeAsync("code-x", ValidOptions());

        foreach (string entry in successLogger.Entries)
        {
            entry.Should().NotContain("super-secret-access");
            entry.Should().NotContain("super-secret-refresh");
        }

        const string errorMarker = "very-distinctive-error-body-marker";
        const string errorBody = "{\"error\":\"invalid_grant\",\"error_description\":\"" + errorMarker + "\"}";
        var (errorClient, _, errorLogger) = CreateClient(HttpStatusCode.BadRequest, errorBody);

        Func<Task> act = () => errorClient.RefreshAccessTokenAsync("some-refresh-token", ValidOptions());
        await act.Should().ThrowAsync<TokenExchangeException>();

        foreach (string entry in errorLogger.Entries)
        {
            entry.Should().NotContain(errorMarker);
            entry.Should().NotContain(errorBody);
        }
    }

    [Theory]
    [InlineData("http://localhost/callback?code=abc", "abc")]
    [InlineData("http://localhost/callback?state=x&code=a%2Bb", "a+b")]
    [InlineData("http://localhost/callback?state=x", null)]
    public void ExtractAuthorizationCode_ParsesCodeFromQuery(string uriString, string? expected)
    {
        var uri = new Uri(uriString);

        string? code = AnafOAuthClient.ExtractAuthorizationCode(uri);

        code.Should().Be(expected);
    }
}
