using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service;
using Service.Configuration;
using Service.Extensions;
using Service.Providers;
using System;
using System.Collections.Generic;
using Xunit;

namespace unit
{
    public class CacheProvider_Should
    {
        [Fact]
        public void CreateRedisCacheProvider_WithCacheOptions()
        {
            // Arrange
            var cacheOptions = new CacheOptions
            {
                Redis = new RedisOptions
                {
                    Endpoint = "localhost",
                    Port = 6379,
                    UseSsl = false
                }
            };

            // Act
            var provider = new RedisCacheProvider(cacheOptions);

            // Assert
            Assert.NotNull(provider);
            var cache = provider.CreateCache();
            Assert.NotNull(cache);
        }

        [Fact]
        public void CreateRedisCacheProvider_WithDefaultConstructor()
        {
            // Act
            var provider = new RedisCacheProvider();

            // Assert
            Assert.NotNull(provider);
            
            // Note: Can't call CreateCache() with default constructor as it would throw due to missing configuration
            Assert.Throws<ArgumentException>(() => provider.CreateCache());
        }



        [Fact]
        public void RedisCacheProvider_HandlesNullOptions_Gracefully()
        {
            // Act
            var provider = new RedisCacheProvider((CacheOptions)null);

            // Assert
            Assert.NotNull(provider);
            // Should throw when trying to create cache with null/empty options
            Assert.Throws<ArgumentException>(() => provider.CreateCache());
        }



        [Fact]
        public void ServiceCollection_RegistersRedisProvider_ByDefault()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["CacheService:DefaultProvider"] = "Redis",
                ["CacheService:Redis:Endpoint"] = "localhost",
                ["CacheService:Redis:Port"] = "6379"
            });

            // Act
            services.AddCacheService(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cacheProvider = serviceProvider.GetRequiredService<ICacheProvider>();
            Assert.IsType<RedisCacheProvider>(cacheProvider);
        }


        [Fact]
        public void ServiceCollection_RegistersICacheTransient()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["CacheService:Redis:Endpoint"] = "localhost"
            });

            // Act
            services.AddCacheService(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cache1 = serviceProvider.GetRequiredService<ICache>();
            var cache2 = serviceProvider.GetRequiredService<ICache>();
            
            Assert.NotNull(cache1);
            Assert.NotNull(cache2);
            // Since ICache is registered as transient, instances should be different
            Assert.NotSame(cache1, cache2);
        }

        [Fact]
        public void ServiceCollection_RegistersICacheProviderSingleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["CacheService:Redis:Endpoint"] = "localhost"
            });

            // Act
            services.AddCacheService(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var provider1 = serviceProvider.GetRequiredService<ICacheProvider>();
            var provider2 = serviceProvider.GetRequiredService<ICacheProvider>();
            
            Assert.NotNull(provider1);
            Assert.NotNull(provider2);
            // Since ICacheProvider is registered as singleton, instances should be the same
            Assert.Same(provider1, provider2);
        }

        [Fact]
        public void ServiceCollection_ThrowsException_WhenConfigurationSectionNotFound()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => services.AddCacheService(configuration));
        }

        [Fact]
        public void ServiceCollection_SupportsCustomSectionName()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["MyCustomSection:Redis:Endpoint"] = "localhost"
            });

            // Act
            services.AddCacheService(configuration, "MyCustomSection");
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cacheProvider = serviceProvider.GetRequiredService<ICacheProvider>();
            Assert.IsType<RedisCacheProvider>(cacheProvider);
        }

        [Fact]
        public void ServiceCollection_WithActionConfiguration_Works()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddCacheService(options =>
            {
                options.Redis.Endpoint = "test-redis";
                options.Redis.Port = 6380;
                options.Redis.UseSsl = true;
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cacheProvider = serviceProvider.GetRequiredService<ICacheProvider>();
            Assert.IsType<RedisCacheProvider>(cacheProvider);
        }

        [Fact]
        public void ServiceCollection_WithCacheOptionsObject_Works()
        {
            // Arrange
            var services = new ServiceCollection();
            var cacheOptions = new CacheOptions
            {
                Redis = new RedisOptions
                {
                    Endpoint = "test-redis",
                    Port = 6380,
                    UseSsl = true
                }
            };

            // Act
            services.AddCacheService(cacheOptions);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cacheProvider = serviceProvider.GetRequiredService<ICacheProvider>();
            Assert.IsType<RedisCacheProvider>(cacheProvider);
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> configValues)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
        }
    }
}