using Microsoft.Extensions.DependencyInjection;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;
using RoEFactura.Utilities;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoEFactura(this IServiceCollection services)
    {
        // Register HTTP clients for API access
        services.AddHttpClient<AnafEInvoiceClient>();
        services.AddHttpClient<OAuthHttpClient>();

        // Register other services
        services.AddTransient<AnafOAuthClient>();
        services.AddTransient<XmlFileDeserializer>();
        return services;
    }
}
