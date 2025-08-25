using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoEFactura.Infrastructure.Data;
using RoEFactura.Mapping.Profiles;
using RoEFactura.Repositories;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;
using RoEFactura.Services.Processing;
using RoEFactura.Utilities;
using RoEFactura.Validation;
using UblSharp;

namespace RoEFactura;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoEFactura(this IServiceCollection services, IConfiguration configuration)
    {
        // Register EF Core DbContext
        services.AddDbContext<InvoiceDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("RoEFactura")));

        // Register AutoMapper with our profiles
        services.AddAutoMapper(typeof(UblToDomainProfile), typeof(DomainToUblProfile));

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<RoCiusUblValidator>();
        services.AddScoped<IValidator<InvoiceType>, RoCiusUblValidator>();

        // Register HTTP clients for API access
        services.AddHttpClient<AnafEInvoiceClient>();

        // Register repositories
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        // Register processing services
        services.AddScoped<UblProcessingService>();

        // Register existing services
        services.AddTransient<AnafOAuthClient>();
        services.AddTransient<XmlFileDeserializer>();

        return services;
    }

    /// <summary>
    /// Overload for backward compatibility without configuration
    /// </summary>
    public static IServiceCollection AddRoEFactura(this IServiceCollection services)
    {
        // Register AutoMapper with our profiles
        services.AddAutoMapper(typeof(UblToDomainProfile), typeof(DomainToUblProfile));

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<RoCiusUblValidator>();
        services.AddScoped<IValidator<InvoiceType>, RoCiusUblValidator>();

        // Register HTTP clients for API access
        services.AddHttpClient<AnafEInvoiceClient>();

        // Register processing services
        services.AddScoped<UblProcessingService>();

        // Register existing services
        services.AddTransient<AnafOAuthClient>();
        services.AddTransient<XmlFileDeserializer>();

        return services;
    }
}
