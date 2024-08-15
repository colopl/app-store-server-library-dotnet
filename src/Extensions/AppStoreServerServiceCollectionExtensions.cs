using AppStoreServerLibraryDotnet.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppStoreServerLibraryDotnet.Extensions;

public static class AppStoreServerServiceCollectionExtensions
{
    /// <summary>
    /// Configures the AppleOptions section and registers the AppStoreServerAPIClient and SignedDataVerifier services.
    /// </summary>
    public static IServiceCollection AddAppStoreServerLibrary(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppleOptions>(configuration.GetSection(AppleOptions.AppleOptionsSectionName));

        services.AddScoped<IAppStoreServerAPIClient, AppStoreServerAPIClient>();
        services.AddScoped<ISignedDataVerifier, SignedDataVerifier>();

        return services;
    }
}