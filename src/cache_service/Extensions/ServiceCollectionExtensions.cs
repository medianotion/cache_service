using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Service.Configuration;
using Service.Providers;
using System;

namespace Service.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCacheService(this IServiceCollection services)
        {
            services.TryAddSingleton<ICacheProvider, RedisCacheProvider>();
            services.TryAddTransient<ICache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return cacheProvider.CreateCache();
            });
            
            return services;
        }

        public static IServiceCollection AddCacheService(this IServiceCollection services, Action<CacheOptions> configureOptions)
        {
            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            services.Configure(configureOptions);
            services.TryAddSingleton<ICacheProvider, RedisCacheProvider>();
            services.TryAddTransient<ICache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return cacheProvider.CreateCache();
            });

            return services;
        }

        public static IServiceCollection AddCacheService(this IServiceCollection services, CacheOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            services.TryAddSingleton<ICacheProvider>(new RedisCacheProvider(options));
            services.TryAddTransient<ICache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return cacheProvider.CreateCache();
            });

            return services;
        }

        public static IServiceCollection AddCacheService(this IServiceCollection services, IConfiguration configuration, string sectionName = CacheOptions.SectionName)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var configSection = configuration.GetSection(sectionName);
            if (!configSection.Exists())
                throw new InvalidOperationException($"Configuration section '{sectionName}' not found.");

            services.Configure<CacheOptions>(configSection);

            // Register the appropriate provider based on configuration
            services.TryAddSingleton<ICacheProvider>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CacheOptions>>().Value;
                return options.DefaultProvider?.ToLowerInvariant() switch
                {
                    "sqlserver" => new SqlServerCacheProvider(options),
                    _ => new RedisCacheProvider(options)
                };
            });

            services.TryAddTransient<ICache>(provider =>
            {
                var cacheProvider = provider.GetRequiredService<ICacheProvider>();
                return cacheProvider.CreateCache();
            });

            return services;
        }
    }
}