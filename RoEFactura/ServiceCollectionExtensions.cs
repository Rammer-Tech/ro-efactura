using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoEFactura.Models;
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
        // Ensure logging is available (idempotent - safe if already registered)
        services.AddLogging();

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
    /// Adds RoEFactura services with OAuth configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="oauthOptions">OAuth configuration options</param>
    /// <param name="configuration">Optional additional configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRoEFacturaWithOAuth(this IServiceCollection services, 
        AnafOAuthOptions oauthOptions, 
        IConfiguration? configuration = null)
    {
        if (oauthOptions == null)
        {
            throw new ArgumentNullException(nameof(oauthOptions));
        }
        
        if (!oauthOptions.IsValid())
        {
            throw new ArgumentException("Invalid OAuth options provided. Ensure all required properties are set.", nameof(oauthOptions));
        }
        
        // Register base RoEFactura services
        AddRoEFactura(services, configuration);
        
        // Register OAuth options as singleton
        services.AddSingleton(oauthOptions);
        
        return services;
    }
    
    /// <summary>
    /// Adds RoEFactura services with OAuth configuration from IConfiguration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing OAuth settings</param>
    /// <param name="sectionName">Configuration section name (defaults to "AnafOAuth")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRoEFacturaWithOAuth(this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "AnafOAuth")
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        
        // Bind OAuth options from configuration
        AnafOAuthOptions oauthOptions = new AnafOAuthOptions();
        IConfigurationSection section = configuration.GetSection(sectionName);
        section.Bind(oauthOptions);
        
        if (!oauthOptions.IsValid())
        {
            throw new InvalidOperationException(
                $"Invalid OAuth configuration found in '{sectionName}' section. " +
                "Ensure ClientId, ClientSecret, and RedirectUri are properly configured.");
        }
        
        return AddRoEFacturaWithOAuth(services, oauthOptions, configuration);
    }
}
