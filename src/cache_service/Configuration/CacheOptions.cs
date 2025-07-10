using System;
using System.Collections.Generic;

namespace Service.Configuration
{
    public class CacheOptions
    {
        public const string SectionName = "CacheService";
        
        public string DefaultProvider { get; set; } = "Redis";
        public Dictionary<string, ProviderOptions> Providers { get; set; } = new Dictionary<string, ProviderOptions>();
        public RedisOptions Redis { get; set; } = new RedisOptions();
        public SqlServerOptions SqlServer { get; set; } = new SqlServerOptions();
    }

    public class ProviderOptions
    {
        public string Type { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }

    public class RedisOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public int Port { get; set; } = 6379;
        public bool UseSsl { get; set; } = false;
        public bool AbortOnConnectFail { get; set; } = false;
        public int Database { get; set; } = 0;
        public RetryOptions Retry { get; set; } = new RetryOptions();
    }

    public class RetryOptions
    {
        public int MaxRetries { get; set; } = 3;
        public int DelaySeconds { get; set; } = 2;
        public bool Enabled { get; set; } = true;
    }

    public class SqlServerOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = "CacheItems";
        public string SchemaName { get; set; } = "dbo";
        public int CleanupIntervalMinutes { get; set; } = 15;
        public RetryOptions Retry { get; set; } = new RetryOptions();
    }
}