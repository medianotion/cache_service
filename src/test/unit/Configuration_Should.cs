using Service.Configuration;
using System.Collections.Generic;
using Xunit;

namespace unit
{
    public class Configuration_Should
    {
        [Fact]
        public void CacheOptions_HasCorrectDefaults()
        {
            // Arrange & Act
            var options = new CacheOptions();

            // Assert
            Assert.Equal("Redis", options.DefaultProvider);
            Assert.NotNull(options.Providers);
            Assert.Empty(options.Providers);
            Assert.NotNull(options.Redis);
            Assert.NotNull(options.SqlServer);
            Assert.Equal("CacheService", CacheOptions.SectionName);
        }

        [Fact]
        public void RedisOptions_HasCorrectDefaults()
        {
            // Arrange & Act
            var options = new RedisOptions();

            // Assert
            Assert.Equal(string.Empty, options.ConnectionString);
            Assert.Equal(string.Empty, options.Endpoint);
            Assert.Equal(6379, options.Port);
            Assert.False(options.UseSsl);
            Assert.False(options.AbortOnConnectFail);
            Assert.Equal(0, options.Database);
            Assert.NotNull(options.Retry);
        }


        [Fact]
        public void RetryOptions_HasCorrectDefaults()
        {
            // Arrange & Act
            var options = new RetryOptions();

            // Assert
            Assert.Equal(3, options.MaxRetries);
            Assert.Equal(2, options.DelaySeconds);
            Assert.True(options.Enabled);
        }

        [Fact]
        public void ProviderOptions_HasCorrectDefaults()
        {
            // Arrange & Act
            var options = new ProviderOptions();

            // Assert
            Assert.Equal(string.Empty, options.Type);
            Assert.True(options.Enabled);
            Assert.NotNull(options.Settings);
            Assert.Empty(options.Settings);
        }

        [Fact]
        public void CacheOptions_AllowsProviderCustomization()
        {
            // Arrange
            var options = new CacheOptions();

            // Act
            options.DefaultProvider = "SqlServer";
            options.Providers.Add("Custom", new ProviderOptions
            {
                Type = "CustomType",
                Enabled = false,
                Settings = new Dictionary<string, string>
                {
                    ["Setting1"] = "Value1",
                    ["Setting2"] = "Value2"
                }
            });

            // Assert
            Assert.Equal("SqlServer", options.DefaultProvider);
            Assert.Single(options.Providers);
            Assert.True(options.Providers.ContainsKey("Custom"));
            
            var customProvider = options.Providers["Custom"];
            Assert.Equal("CustomType", customProvider.Type);
            Assert.False(customProvider.Enabled);
            Assert.Equal(2, customProvider.Settings.Count);
            Assert.Equal("Value1", customProvider.Settings["Setting1"]);
            Assert.Equal("Value2", customProvider.Settings["Setting2"]);
        }

        [Fact]
        public void RedisOptions_AllowsCustomConfiguration()
        {
            // Arrange
            var options = new RedisOptions();

            // Act
            options.ConnectionString = "localhost:6380";
            options.Endpoint = "redis.example.com";
            options.Port = 6380;
            options.UseSsl = true;
            options.AbortOnConnectFail = true;
            options.Database = 2;
            options.Retry.MaxRetries = 5;
            options.Retry.DelaySeconds = 3;
            options.Retry.Enabled = false;

            // Assert
            Assert.Equal("localhost:6380", options.ConnectionString);
            Assert.Equal("redis.example.com", options.Endpoint);
            Assert.Equal(6380, options.Port);
            Assert.True(options.UseSsl);
            Assert.True(options.AbortOnConnectFail);
            Assert.Equal(2, options.Database);
            Assert.Equal(5, options.Retry.MaxRetries);
            Assert.Equal(3, options.Retry.DelaySeconds);
            Assert.False(options.Retry.Enabled);
        }

    }
}