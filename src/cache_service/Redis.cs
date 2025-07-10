using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using System.Linq;
using Polly.Retry;
using System.Diagnostics.CodeAnalysis;
using Service.Configuration;

namespace Service
{
    internal class Redis : ICache
    {
        private const int DefaultPort = 6379;
        private const int DefaultRetries = 3;
        private const int RetryDelaySeconds = 2; 


        private ConnectionMultiplexer _redis { get; set; }
        private IDatabase _db { get; set; }
        private IServer _server {  get; set; }
        private Dictionary<string, int> _ttlPerKey { get; set; }
        private AsyncRetryPolicy _retryPolicy {get;set;}

        [ExcludeFromCodeCoverage]
        public Redis(string endpoint, bool useSSL, int port = DefaultPort, int retries = DefaultRetries)
        {
            ConfigurationOptions config = new ConfigurationOptions();
            config.EndPoints.Add(endpoint, port);
            config.Ssl = useSSL;
            config.AbortOnConnectFail = false;
            _redis = ConnectionMultiplexer.Connect(config);
            _server = _redis.GetServer($"{endpoint}:{port}");
            _db = _redis.GetDatabase();
            _ttlPerKey = new Dictionary<string, int>();
            _retryPolicy = CacheUtilities.SetupRedisRetryPolicy(retries, RetryDelaySeconds);
        }

        [ExcludeFromCodeCoverage]
        public Redis(RedisOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            ConfigurationOptions config = new ConfigurationOptions();
            
            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                config = ConfigurationOptions.Parse(options.ConnectionString);
            }
            else if (!string.IsNullOrEmpty(options.Endpoint))
            {
                config.EndPoints.Add(options.Endpoint, options.Port);
                config.Ssl = options.UseSsl;
            }
            else
            {
                throw new ArgumentException("Either ConnectionString or Endpoint must be provided");
            }

            config.AbortOnConnectFail = options.AbortOnConnectFail;
            
            _redis = ConnectionMultiplexer.Connect(config);
            _server = _redis.GetServer(_redis.GetEndPoints().First());
            _db = _redis.GetDatabase(options.Database);
            _ttlPerKey = new Dictionary<string, int>();
            
            var retries = options.Retry.Enabled ? options.Retry.MaxRetries : DefaultRetries;
            var delaySeconds = options.Retry.DelaySeconds > 0 ? options.Retry.DelaySeconds : RetryDelaySeconds;
            _retryPolicy = CacheUtilities.SetupRedisRetryPolicy(retries, delaySeconds);
        }

        // used for unit testing
        [ExcludeFromCodeCoverage]
        internal Redis(IDatabase db) 
        { 
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ttlPerKey = new Dictionary<string, int>();
            _retryPolicy = CacheUtilities.SetupRedisRetryPolicy(DefaultRetries, RetryDelaySeconds);
        }
        // used for unit testing
        [ExcludeFromCodeCoverage]
        internal Redis(IServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _ttlPerKey = new Dictionary<string, int>();
            _retryPolicy = CacheUtilities.SetupRedisRetryPolicy(DefaultRetries, RetryDelaySeconds);
        }
        
        public async Task<string> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
             {
                 CacheUtilities.ValidateKey(key);
                 return await _db.StringGetAsync(key);
             });

        }

        public async Task<bool> ExpireKey(string key, int expireInSeconds, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                
                CacheUtilities.ValidateKey(key);
                CacheUtilities.ValidateExpiration(expireInSeconds);
                int oldExpireInSeconds = 0;

                // only sets TTL if the ttl has not yet been set or the ttl has been changed.
                bool setTTL = false;
                if (!_ttlPerKey.ContainsKey(key))
                    setTTL = true;
                else if (_ttlPerKey[key] != expireInSeconds)
                {
                    oldExpireInSeconds = _ttlPerKey[key];
                    setTTL = true;
                }


                if (setTTL)
                {
                    await _db.KeyExpireAsync(key, new TimeSpan(0, 0, expireInSeconds));

                    if (oldExpireInSeconds > 0)
                        _ttlPerKey[key] = expireInSeconds;
                    else
                        _ttlPerKey.Add(key, expireInSeconds);

                    return true;
                }
                else
                    return false;
            });

        }

       public async Task<bool> SetAsync(string key, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                CacheUtilities.ValidateExpiration(expireInSeconds);

                When overwrite = When.Always;
                if (!create_or_overwrite)
                    overwrite = When.NotExists;

                return await _db.StringSetAsync(key, value, new TimeSpan(0, 0, expireInSeconds), overwrite);
            });
        }

        public async Task<bool> SetIfNotExistsAsync(string key, string value, int expireInSeconds, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                CacheUtilities.ValidateExpiration(expireInSeconds);

                return await _db.StringSetAsync(key, value, new TimeSpan(0, 0, expireInSeconds), When.NotExists);
            });
        }

        public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.KeyDeleteAsync(key);
            });
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var redisKeys = keys.Select(key => new RedisKey(key));

                foreach (string key in keys)
                {
                    CacheUtilities.ValidateKey(key);
                }
                var keysRemovedCount = await _db.KeyDeleteAsync(redisKeys.ToArray());
                return keysRemovedCount == keys.LongCount();
            });
        }

        public async Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.StringIncrementAsync(key);
            });
        }

        public async Task<long> IncrementByAsync(string key, long number, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.StringIncrementAsync(key, number);
            });
        }

        public async Task<long> DecrementAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.StringDecrementAsync(key);
            });
        }

        public async Task<long> DecrementByAsync(string key, long number, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.StringDecrementAsync(key, number);
            });
        }

        public async Task<long> LengthOfListAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.ListLengthAsync(key);
            });
        }

        public async Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                var result = await _db.ListRangeAsync(key);
                return new List<string>(result.ToStringArray());
            });

        }

        public async Task<long> AddToListAsync(string key, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                CacheUtilities.ValidateExpiration(expireInSeconds);

                When overwrite = When.Always;
                if (!create_or_overwrite)
                    overwrite = When.NotExists;

                var result = await _db.ListRightPushAsync(key, value, overwrite);
                await ExpireKey(key, expireInSeconds);
                return result;



            });
        }

        public async Task<long> RemoveFromListAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.ListRemoveAsync(key, value);
            });
        }

        public async Task<bool> AddToSetAsync(string key, string value, int expireInSeconds, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                CacheUtilities.ValidateExpiration(expireInSeconds);

                var result = await _db.SetAddAsync(key, value);
                await ExpireKey(key, expireInSeconds);
                return result;

            });

        }


        public async Task<List<string>> GetSetAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                var r = await _db.SetMembersAsync(key);
                return new List<string>(r.ToStringArray());

            });

        }

        public async Task<long> LengthOfSetAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.SetLengthAsync(key);
            });

        }
        public async Task<bool> RemoveFromSetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.SetRemoveAsync(key, value.ToString());
            });

        }

        public async Task<bool> AddToHashAsync(string key, string field, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                CacheUtilities.ValidateExpiration(expireInSeconds);

                var result = await _db.HashSetAsync(key, field, value);
                await ExpireKey(key, expireInSeconds);
                return result;

            });

        }


        public async Task<string> GetHashAsync(string key, string field, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.HashGetAsync(key, field);
            });

        }

        public async Task<Dictionary<string, string>> GetHashAllAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                var r = await _db.HashGetAllAsync(key);
                
                if (r == null || r.Length == 0)
                    return new Dictionary<string, string>();

                var result = new Dictionary<string, string>();
                foreach (var entry in r)
                    result.Add(entry.Name, entry.Value);
                return result;
            });
        }

        public async Task<long> LengthOfHashAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.HashLengthAsync(key);
            });

        }
        public async Task<bool> RemoveFromHashAsync(string key, string fieldKey, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                return await _db.HashDeleteAsync(key, fieldKey);
            });

        }




    }

}

