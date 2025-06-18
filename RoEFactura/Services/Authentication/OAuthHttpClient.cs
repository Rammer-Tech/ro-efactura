using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace RoEFactura.Services.Authentication;

public class OAuthHttpClient
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public OAuthHttpClient(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        string tokenUrl = _configuration["OAuth:TokenUrl"];
        string clientId = _configuration["OAuth:ClientId"];
        string clientSecret = _configuration["OAuth:ClientSecret"];
        string callbackUrl = _configuration["OAuth:CallbackUrl"];

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", "authorization_code_from_previous_step" },
                { "redirect_uri", callbackUrl },
                { "client_id", clientId },
                { "client_secret", clientSecret }
            })
        };

        HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string payloadString = await response.Content.ReadAsStringAsync();
        Dictionary<string, string> payload = JsonConvert.DeserializeObject<Dictionary<string, string>>(payloadString);

        return payload["access_token"];
    }

    public async Task<string> GetDataAsync(string accessToken)
    {
        string resourceUrl = _configuration["OAuth:ResourceUrl"];

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await _httpClient.GetAsync(resourceUrl);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}