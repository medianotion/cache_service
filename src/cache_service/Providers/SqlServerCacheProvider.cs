using Microsoft.Extensions.Options;
using Service.Configuration;

namespace Service.Providers
{
    public class SqlServerCacheProvider : ICacheProvider
    {
        private readonly SqlServerOptions _options;

        public SqlServerCacheProvider(IOptions<CacheOptions> options)
        {
            _options = options?.Value?.SqlServer ?? throw new System.ArgumentNullException(nameof(options));
        }

        public SqlServerCacheProvider(SqlServerOptions options)
        {
            _options = options ?? throw new System.ArgumentNullException(nameof(options));
        }

        public SqlServerCacheProvider(CacheOptions options)
        {
            _options = options?.SqlServer ?? throw new System.ArgumentNullException(nameof(options));
        }

        public ICache CreateCache()
        {
            return new SqlServer(_options);
        }

        public ICache CreateCache(CacheOptions options)
        {
            return new SqlServer(options?.SqlServer ?? throw new System.ArgumentNullException(nameof(options)));
        }
    }
}