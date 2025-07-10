using Microsoft.Extensions.Options;
using Service.Configuration;
using System;

namespace Service.Providers
{
    public class RedisCacheProvider : ICacheProvider
    {
        private readonly CacheOptions _defaultOptions;

        public RedisCacheProvider()
        {
            _defaultOptions = new CacheOptions();
        }

        public RedisCacheProvider(IOptions<CacheOptions> options)
        {
            _defaultOptions = options?.Value ?? new CacheOptions();
        }

        public RedisCacheProvider(CacheOptions options)
        {
            _defaultOptions = options ?? new CacheOptions();
        }

        public ICache CreateCache(CacheOptions options)
        {
            if (options?.Redis == null)
                throw new ArgumentException("Redis configuration is required", nameof(options));

            var redisOptions = options.Redis;
            
            if (string.IsNullOrEmpty(redisOptions.Endpoint) && string.IsNullOrEmpty(redisOptions.ConnectionString))
                throw new ArgumentException("Either Redis Endpoint or ConnectionString must be provided");

            return new Redis(redisOptions);
        }

        public ICache CreateCache()
        {
            return CreateCache(_defaultOptions);
        }
    }
}