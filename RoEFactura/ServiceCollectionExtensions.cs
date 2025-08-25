using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;
using RoEFactura.Services.Processing;
using RoEFactura.Utilities;
using RoEFactura.Validation;
using UblSharp;

namespace RoEFactura;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoEFactura(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<RoCiusUblValidator>();
        services.AddScoped<IValidator<InvoiceType>, RoCiusUblValidator>();

        // Register HTTP clients for API access
        services.AddHttpClient<AnafEInvoiceClient>();

        // Register HttpClient factory for ANAF OAuth  
        services.AddHttpClient();

        // Register service interfaces with internal implementations
        services.AddScoped<IAnafOAuthClient, AnafOAuthClient>();
        services.AddScoped<IAnafEInvoiceClient, AnafEInvoiceClient>();
        services.AddScoped<IUblProcessingService, UblProcessingService>();
        
        // Register utilities
        services.AddTransient<XmlFileDeserializer>();

        return services;
    }

    /// <summary>
    /// Overload for backward compatibility without configuration
    /// </summary>
    public static IServiceCollection AddRoEFactura(this IServiceCollection services)
    {
        return AddRoEFactura(services, null);
    }
}
