using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace RoEFactura.Services.Authentication;

public class AnafOAuthClient
{
    private readonly IConfiguration _configuration;

    public AnafOAuthClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<Token> GetAccessTokenAsync(string clientId, string clientSecret, string callbackUrl)
    {
        using HttpClient client = CreateClientWithCertificate();

        return await GetJwtTokenAsync(clientId, clientSecret, callbackUrl, client);
    }

    public async Task<Token> GetAccessTokenAsync(X509Certificate2 certificate, string clientId, string clientSecret,
        string callbackUrl)
    {
        using HttpClient client = CreateClientWithCertificate(certificate);

        return await GetJwtTokenAsync(clientId, clientSecret, callbackUrl, client);
    }

    private static async Task<Token> GetJwtTokenAsync(string clientId, string clientSecret, string callbackUrl,
        HttpClient client)
    {
        string url =
            $"https://logincert.anaf.ro/anaf-oauth2/v1/authorize?" +
            $"response_type=code&" +
            $"client_id={clientId}&" +
            $"redirect_uri={Uri.EscapeDataString(callbackUrl)}&" +
            $"token_content_type=jwt";

        HttpResponseMessage response = await client.GetAsync(url);

        if (response.RequestMessage?.RequestUri?.Query == null ||
            !response.RequestMessage.RequestUri.Query.Contains("code="))
        {
            throw new Exception("Authorization code not received.");
        }

        string code = response.RequestMessage.RequestUri.Query.Replace("?code=", "");
        string postData =
            $"grant_type=authorization_code&" +
            $"code={code}&" +
            $"client_id={clientId}&" +
            $"client_secret={clientSecret}&" +
            $"redirect_uri={Uri.EscapeDataString(callbackUrl)}&" +
            $"token_content_type=jwt";

        response = await client.PostAsync("https://logincert.anaf.ro/anaf-oauth2/v1/token",
            new StringContent(postData));
        string resultContent = await response.Content.ReadAsStringAsync();

        if (!resultContent.Contains("access_token"))
        {
            throw new Exception("Token not received. Response: " + resultContent);
        }

        return JsonSerializer.Deserialize<Token>(resultContent);
    }

    private static HttpClient CreateClientWithCertificate()
    {
        HttpClientHandler handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            UseProxy = false
        };

        X509Certificate2 cert = GetCertificate();
        handler.ClientCertificates.Add(cert);

        return new HttpClient(handler);
    }

    private static HttpClient CreateClientWithCertificate(X509Certificate2 certificate)
    {
        var cert = GetCertificate();

        HttpClientHandler handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            UseProxy = false,
            ClientCertificates = { certificate }
        };

        return new HttpClient(handler);
    }

    private static X509Certificate2 GetCertificate(bool getAll = false)
    {
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2Collection foundCertificates = store.Certificates;
        X509Certificate2Collection validCertificates =
            foundCertificates.Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: false);

        X509Certificate2Collection selectedCertificates = [];

        foreach (X509Certificate2 cert in validCertificates)
        {
            string certificateIssuerName = cert.IssuerName.Name;

            if (getAll || certificateIssuerName.Contains("CERTSIGN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("DIGISIGN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("TRAN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("ALFASIGN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("CERT DIGI", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("CERTDIGITAL", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("DE CALCUL", StringComparison.CurrentCultureIgnoreCase))
            {
                selectedCertificates.Add(cert);
            }
        }

        store.Close(); // Ensure the store is always closed after use

        return selectedCertificates.Count switch
        {
            // Handling multiple or single certificate scenarios:
            1 => selectedCertificates[0],
            > 1 => throw new InvalidOperationException(
                "Multiple certificates found. Unable to determine which one to use."),
            _ => throw new InvalidOperationException("No valid certificates were found.")
        };

        // If no certificates were found or selected, handle this case appropriately:
    }
}

public class Token
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string TokenType { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; }
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }
}