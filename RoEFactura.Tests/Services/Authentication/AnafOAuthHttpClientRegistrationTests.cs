using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoEFactura.Models;
using RoEFactura.Services.Authentication;
using Xunit;

namespace RoEFactura.Tests.Services.Authentication;

/// <summary>
/// DI wiring tests for the named ANAF OAuth <see cref="HttpClient"/> (<see cref="RoEFacturaHttpClients.AnafOAuth"/>):
/// the name is a stable contract value, every <c>AddRoEFactura*</c> overload registers it, and the OAuth token
/// requests (refresh and authorization-code exchange) are sent through it and through nothing else.
/// </summary>
public class AnafOAuthHttpClientRegistrationTests
{
    // Same payload as AnafOAuthClientTests.SuccessTokenJson (that constant is private to its test class).
    private const string SuccessTokenJson =
        "{\"access_token\":\"access-abc\",\"refresh_token\":\"refresh-xyz\",\"expires_in\":5184000,\"token_type\":\"Bearer\",\"scope\":\"e-factura\"}";

    private static AnafOAuthOptions ValidOptions() => new()
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        RedirectUri = "https://app.example.test/callback",
        TokenUrl = "https://oauth.example.test/token",
    };

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configureServices)
    {
        ServiceCollection services = new();
        configureServices(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AnafOAuthClientName_IsStableContractValue()
    {
        // The literal is the cross-repo contract: consumers configure the named client by this exact value.
        RoEFacturaHttpClients.AnafOAuth.Should().Be("RoEFactura.AnafOAuth");
    }

    // The two *_FactoryCreatesNamedAnafOAuthClient tests are smoke tests only: IHttpClientFactory returns a client for
    // any name, registered or not. The real guards are the two *_ViaDi_SendsThroughNamedClientOnly tests below (they
    // discriminate named from unnamed) and the registration grep on ServiceCollectionExtensions.
    [Fact]
    public void AddRoEFactura_FactoryCreatesNamedAnafOAuthClient()
    {
        using ServiceProvider provider = BuildProvider(s => s.AddRoEFactura());
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpClient client = factory.CreateClient(RoEFacturaHttpClients.AnafOAuth);

        client.Should().NotBeNull();
    }

    [Fact]
    public void AddRoEFacturaWithOAuth_FactoryCreatesNamedAnafOAuthClient()
    {
        using ServiceProvider provider = BuildProvider(s => s.AddRoEFacturaWithOAuth(ValidOptions()));
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpClient client = factory.CreateClient(RoEFacturaHttpClients.AnafOAuth);

        client.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_ViaDi_SendsThroughNamedClientOnly()
    {
        var defaultHandler = OAuthRecordingHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, "{}");
        var namedHandler = OAuthRecordingHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessTokenJson);
        await using ServiceProvider provider = BuildProvider(s =>
        {
            s.AddRoEFactura();
            s.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => defaultHandler);
            s.AddHttpClient(RoEFacturaHttpClients.AnafOAuth).ConfigurePrimaryHttpMessageHandler(() => namedHandler);
        });
        using IServiceScope scope = provider.CreateScope();
        IAnafOAuthClient oauthClient = scope.ServiceProvider.GetRequiredService<IAnafOAuthClient>();

        Token token = await oauthClient.RefreshAccessTokenAsync("refresh-token-1", ValidOptions(), CancellationToken.None);

        token.AccessToken.Should().Be("access-abc");
        namedHandler.RequestCount.Should().Be(1);
        defaultHandler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_ViaDi_SendsThroughNamedClientOnly()
    {
        var defaultHandler = OAuthRecordingHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, "{}");
        var namedHandler = OAuthRecordingHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessTokenJson);
        await using ServiceProvider provider = BuildProvider(s =>
        {
            s.AddRoEFactura();
            s.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => defaultHandler);
            s.AddHttpClient(RoEFacturaHttpClients.AnafOAuth).ConfigurePrimaryHttpMessageHandler(() => namedHandler);
        });
        using IServiceScope scope = provider.CreateScope();
        IAnafOAuthClient oauthClient = scope.ServiceProvider.GetRequiredService<IAnafOAuthClient>();

        Token token = await oauthClient.ExchangeAuthorizationCodeAsync("auth-code-1", ValidOptions(), CancellationToken.None);

        token.AccessToken.Should().Be("access-abc");
        namedHandler.RequestCount.Should().Be(1);
        defaultHandler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_NamedClientTimeout_SendsExactlyOneRequest()
    {
        var hangingHandler = OAuthRecordingHttpMessageHandler.Hanging();
        await using ServiceProvider provider = BuildProvider(s =>
        {
            s.AddRoEFactura();
            s.AddHttpClient(RoEFacturaHttpClients.AnafOAuth)
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMilliseconds(250))
                .ConfigurePrimaryHttpMessageHandler(() => hangingHandler);
        });
        using IServiceScope scope = provider.CreateScope();
        IAnafOAuthClient oauthClient = scope.ServiceProvider.GetRequiredService<IAnafOAuthClient>();

        Func<Task> act = () => oauthClient.RefreshAccessTokenAsync("refresh-token-1", ValidOptions(), CancellationToken.None);

        (await act.Should().ThrowAsync<TokenExchangeException>())
            .Which.ErrorType.Should().Be(TokenExchangeErrorType.Timeout);
        hangingHandler.RequestCount.Should().Be(1);
    }
}
