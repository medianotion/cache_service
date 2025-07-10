using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Service;
using Service.Configuration;
using Service.Extensions;
using Service.Providers;
using System;
using System.Collections.Generic;
using Xunit;

namespace unit
{
    public class SqlServer_Should
    {
        [Fact]
        public void SqlServerOptions_HasCorrectDefaults()
        {
            // Arrange & Act
            var options = new SqlServerOptions();

            // Assert
            Assert.Equal(string.Empty, options.ConnectionString);
            Assert.Equal("CacheItems", options.TableName);
            Assert.Equal("dbo", options.SchemaName);
            Assert.Equal(15, options.CleanupIntervalMinutes);
            Assert.NotNull(options.Retry);
        }

        [Fact]
        public void SqlServerOptions_AllowsCustomConfiguration()
        {
            // Arrange
            var options = new SqlServerOptions();

            // Act
            options.ConnectionString = "Server=localhost;Database=MyCache;Trusted_Connection=true;";
            options.TableName = "MyTable";
            options.SchemaName = "cache";
            options.CleanupIntervalMinutes = 30;
            options.Retry.MaxRetries = 2;
            options.Retry.DelaySeconds = 1;
            options.Retry.Enabled = false;

            // Assert
            Assert.Equal("Server=localhost;Database=MyCache;Trusted_Connection=true;", options.ConnectionString);
            Assert.Equal("MyTable", options.TableName);
            Assert.Equal("cache", options.SchemaName);
            Assert.Equal(30, options.CleanupIntervalMinutes);
            Assert.Equal(2, options.Retry.MaxRetries);
            Assert.Equal(1, options.Retry.DelaySeconds);
            Assert.False(options.Retry.Enabled);
        }

        [Fact]
        public void CreateSqlServerCacheProvider_WithOptions()
        {
            // Arrange
            var sqlOptions = new SqlServerOptions
            {
                ConnectionString = "Server=localhost;Database=TestDB;Trusted_Connection=true;",
                TableName = "TestCache",
                SchemaName = "dbo"
            };
            var cacheOptions = new CacheOptions { SqlServer = sqlOptions };

            // Act
            var provider = new SqlServerCacheProvider(cacheOptions);

            // Assert
            Assert.NotNull(provider);
            // Note: We don't call CreateCache() here because it would try to connect to SQL Server
        }

        [Fact]
        public void CreateSqlServerCacheProvider_WithCacheOptions()
        {
            // Arrange
            var cacheOptions = new CacheOptions
            {
                SqlServer = new SqlServerOptions
                {
                    ConnectionString = "Server=localhost;Database=TestDB;Trusted_Connection=true;",
                    TableName = "TestCache",
                    SchemaName = "dbo"
                }
            };

            // Act
            var provider = new SqlServerCacheProvider(cacheOptions);

            // Assert
            Assert.NotNull(provider);
        }

        [Fact]
        public void SqlServerCacheProvider_ThrowsException_WhenOptionsIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SqlServerCacheProvider((CacheOptions)null));
        }

        [Fact]
        public void SqlServerCacheProvider_ThrowsException_WithTableScript_WhenTableDoesNotExist()
        {
            // Arrange
            var cacheOptions = new CacheOptions
            {
                SqlServer = new SqlServerOptions
                {
                    ConnectionString = "Server=nonexistent;Database=TestDB;Trusted_Connection=true;",
                    TableName = "TestCache",
                    SchemaName = "dbo"
                }
            };

            // Act
            var provider = new SqlServerCacheProvider(cacheOptions);
            
            // Assert - The error should occur when creating the cache, not when creating the provider
            var ex = Assert.Throws<InvalidOperationException>(() => provider.CreateCache());
            
            // Verify the error message contains the table creation script
            Assert.Contains("CREATE TABLE [dbo].[TestCache]", ex.Message);
            Assert.Contains("CREATE INDEX IX_TestCache_ExpiresAt", ex.Message);
            Assert.Contains("cache table [dbo].[TestCache]", ex.Message);
        }

        [Fact]
        public void ServiceCollection_RegistersSqlServerProvider_WhenConfigured()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                ["CacheService:DefaultProvider"] = "SqlServer",
                ["CacheService:SqlServer:ConnectionString"] = "Server=localhost;Database=TestDB;Trusted_Connection=true;"
            });

            // Act
            services.AddCacheService(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var cacheProvider = serviceProvider.GetRequiredService<ICacheProvider>();
            Assert.IsType<SqlServerCacheProvider>(cacheProvider);
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> configValues)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
        }
    }
}