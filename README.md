# Cache Service for .NET Standard

A cache service library for .NET Standard applications.

## Features

- **Redis Integration**: Built on StackExchange.Redis for reliable Redis connectivity
- **SQL Server Integration**: Built on System.Data.SqlClient for SQL Server caching
- **Automatic Retry**: Uses Polly retry policies for RedisServerException and RedisTimeoutException (default: 3 retries, configurable)
- **Multiple Data Types**: Support for strings, lists, sets, hashes, and counters
- **Automatic TTL Management**: All "set" operations require `expireInSeconds` parameter for automatic key expiration
- **Cancellation Support**: All async operations support `CancellationToken` for cooperative cancellation
- **Easy to Use**: Simple provider pattern for quick setup

## Important Notes

- All "setting" methods (AddToSetAsync, SetStringAsync, etc.) require an `expireInSeconds` parameter
- The library automatically manages TTL (Time To Live) for all keys
- Keys expire automatically to conserve Redis memory—choose appropriate expiration times
- Avoid calling ExpireKey explicitly unless updating TTL without changing values


## Usage

### Provider Pattern with Dependency Injection

The cache service supports multiple configuration approaches, including configuration from appsettings.json files.

#### Configuration from appsettings.json

**appsettings.json example:**

```json
{
  "CacheService": {
    "DefaultProvider": "Redis",
    "Providers": {
      "Redis": {
        "Type": "Redis",
        "Enabled": true,
        "Settings": {
          "Description": "Primary Redis cache"
        }
      }
    },
    "Redis": {
      "ConnectionString": "localhost:6379",
      "UseSsl": false,
      "AbortOnConnectFail": false,
      "Database": 0,
      "Retry": {
        "MaxRetries": 3,
        "DelaySeconds": 2,
        "Enabled": true
      }
    }
  }
}
```

**Alternative appsettings.json using separate endpoint/port:**

```json
{
  "CacheService": {
    "Redis": {
      "Endpoint": "redis.example.com",
      "Port": 6380,
      "UseSsl": true,
      "Database": 1,
      "Retry": {
        "MaxRetries": 5,
        "DelaySeconds": 1,
        "Enabled": true
      }
    }
  }
}
```

**SQL Server appsettings.json example:**

```json
{
  "CacheService": {
    "DefaultProvider": "SqlServer",
    "Providers": {
      "SqlServer": {
        "Type": "SqlServer",
        "Enabled": true,
        "Settings": {
          "Description": "Primary SQL Server cache"
        }
      }
    },
    "SqlServer": {
      "ConnectionString": "Server=localhost;Database=CacheDB;Trusted_Connection=true;",
      "TableName": "CacheItems",
      "SchemaName": "dbo",
      "CleanupIntervalMinutes": 15,
      "Retry": {
        "MaxRetries": 3,
        "DelaySeconds": 2,
        "Enabled": true
      }
    }
  }
}
```

**Alternative SQL Server configuration for production:**

```json
{
  "CacheService": {
    "DefaultProvider": "SqlServer",
    "SqlServer": {
      "ConnectionString": "Server=prod-sql.company.com;Database=ProductionCache;User Id=cache_user;Password=secure_password;Encrypt=true;",
      "TableName": "AppCache",
      "SchemaName": "cache",
      "CleanupIntervalMinutes": 10,
      "Retry": {
        "MaxRetries": 5,
        "DelaySeconds": 1,
        "Enabled": true
      }
    }
  }
}
```

#### Console Application with appsettings.json

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Service.Extensions;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                          optional: true, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        // Configure cache service from appsettings.json
        services.AddCacheService(context.Configuration);

        // Register your application services
        services.AddScoped<MyService>();
    })
    .Build();

// Use the cache service
var myService = host.Services.GetRequiredService<MyService>();
await myService.DoSomethingWithCache();

public class MyService
{
    private readonly ICache _cache;

    public MyService(ICache cache)
    {
        _cache = cache;
    }

    public async Task DoSomethingWithCache()
    {
        await _cache.SetAsync("key", "value", 3600); // 1 hour expiration
        var value = await _cache.GetAsync("key");
        Console.WriteLine($"Retrieved: {value}");
    }
}
```

#### Console Application with SQL Server Cache

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Service.Extensions;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                          optional: true, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        // Configure SQL Server cache service from appsettings.json
        services.AddCacheService(context.Configuration);

        // Register your application services
        services.AddScoped<MyService>();
    })
    .Build();

// Use the cache service
var myService = host.Services.GetRequiredService<MyService>();
await myService.DoSomethingWithSqlCache();

public class MyService
{
    private readonly ICache _cache;

    public MyService(ICache cache)
    {
        _cache = cache;
    }

    public async Task DoSomethingWithSqlCache()
    {
        // SQL Server cache supports strings and counters
        await _cache.SetAsync("user:123", "John Doe", 3600); // 1 hour expiration
        var user = await _cache.GetAsync("user:123");
        Console.WriteLine($"Retrieved user: {user}");

        // Increment counters (works with SQL Server)
        await _cache.IncrementAsync("page:views");
        var views = await _cache.IncrementByAsync("page:views", 5);
        Console.WriteLine($"Page views: {views}");

        // Note: Complex types like lists, sets, hashes throw NotSupportedException
        // Use Redis provider for those operations
    }
}
```

#### ASP.NET Core Application with appsettings.json

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure cache service from appsettings.json (uses "CacheService" section by default)
builder.Services.AddCacheService(builder.Configuration);

// Or specify a custom section name
// builder.Services.AddCacheService(builder.Configuration, "MyCustomCacheSection");

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();

// Controller
[ApiController]
[Route("api/[controller]")]
public class CacheController : ControllerBase
{
    private readonly ICache _cache;

    public CacheController(ICache cache)
    {
        _cache = cache;
    }

    [HttpPost("set")]
    public async Task<IActionResult> SetValue(string key, string value, CancellationToken cancellationToken)
    {
        var success = await _cache.SetAsync(key, value, 3600, cancellationToken: cancellationToken);
        return success ? Ok() : BadRequest();
    }

    [HttpGet("get/{key}")]
    public async Task<IActionResult> GetValue(string key, CancellationToken cancellationToken)
    {
        var value = await _cache.GetAsync(key, cancellationToken);
        return value != null ? Ok(value) : NotFound();
    }
}
```

#### Provider Comparison and SQL Server Considerations

**SQL Server Cache Provider Features:**
- ✅ String operations (`GetAsync`, `SetAsync`, `SetIfNotExistsAsync`, `DeleteAsync`)
- ✅ Counter operations (`IncrementAsync`, `DecrementAsync`, `IncrementByAsync`, `DecrementByAsync`)
- ✅ Key expiration and automatic cleanup
- ✅ Retry policies with Polly
- ⚠️ Manual table creation required
- ❌ Lists, Sets, Hashes (use Redis for these)

**Redis Cache Provider Features:**
- ✅ All string and counter-operations
- ✅ Lists (`AddToListAsync`, `GetListAsync`, `RemoveFromListAsync`)
- ✅ Sets (`AddToSetAsync`, `GetSetAsync`, `RemoveFromSetAsync`)
- ✅ Hashes (`AddToHashAsync`, `GetHashAsync`, `GetHashAllAsync`)
- ✅ All advanced Redis data types

**When to Use SQL Server Cache:**
- When you already have SQL Server infrastructure
- For simple key-value storage and counters
- When you need ACID compliance for cache operations
- For persistent caching across application restarts

**When to Use Redis Cache:**
- When you need advanced data structures (lists, sets, hashes)
- For high-performance scenarios
- When using Redis-specific features
- For distributed applications with multiple cache nodes

**Mixed Provider Usage Example:**

```csharp
// In appsettings.json - you can configure different providers for different use cases
{
  "CacheService": {
    "DefaultProvider": "SqlServer",  // Use SQL Server for general caching
    "SqlServer": {
      "ConnectionString": "Server=localhost;Database=CacheDB;Trusted_Connection=true;"
    }
  },
  "RedisCacheService": {  // Separate configuration for Redis when needed
    "DefaultProvider": "Redis",
    "Redis": {
      "Endpoint": "localhost",
      "Port": 6379
    }
  }
}

// In Program.cs - register both providers with different configurations
services.AddCacheService(Configuration, "CacheService");  // SQL Server for general use
services.AddCacheService(Configuration, "RedisCacheService");  // Redis for complex operations
```

#### Provider Pattern without Dependency Injection (.NET Framework Compatible)

For .NET Framework applications or scenarios where you need to use the cache providers without dependency injection:

**Redis Provider Example:**

```csharp
using Service.Configuration;
using Service.Providers;

// Create Redis cache provider with configuration
var redisOptions = new RedisOptions
{
    Endpoint = "localhost",
    Port = 6379,
    UseSsl = false,
    Database = 0,
    Retry = new RetryOptions
    {
        MaxRetries = 3,
        DelaySeconds = 2,
        Enabled = true
    }
};

var cacheOptions = new CacheOptions
{
    Redis = redisOptions
};

// Create provider and cache instance
var provider = new RedisCacheProvider(cacheOptions);
var cache = provider.CreateCache();

// Use the cache
await cache.SetAsync("key", "value", 3600); // 1 hour expiration
var value = await cache.GetAsync("key");
Console.WriteLine($"Retrieved: {value}");

// With cancellation token for timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try 
{
    await cache.SetAsync("timeout-key", "value", 3600, cancellationToken: cts.Token);
    var timedValue = await cache.GetAsync("timeout-key", cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cache operation timed out");
}
```

**SQL Server Provider Example:**

```csharp
using Service.Configuration;
using Service.Providers;

// Create SQL Server cache provider with configuration
var sqlOptions = new SqlServerOptions
{
    ConnectionString = "Server=localhost;Database=CacheDB;Trusted_Connection=true;",
    TableName = "CacheItems",
    SchemaName = "dbo",
    CleanupIntervalMinutes = 15,
    Retry = new RetryOptions
    {
        MaxRetries = 3,
        DelaySeconds = 2,
        Enabled = true
    }
};

var cacheOptions = new CacheOptions
{
    SqlServer = sqlOptions
};

// Create provider and cache instance
var provider = new SqlServerCacheProvider(cacheOptions);
var cache = provider.CreateCache();

// Use the cache (SQL Server supports strings and counters)
await cache.SetAsync("user:123", "John Doe", 3600); // 1 hour expiration
var user = await cache.GetAsync("user:123");
Console.WriteLine($"Retrieved user: {user}");

// Counter operations
await cache.IncrementAsync("page:views");
var views = await cache.IncrementByAsync("page:views", 5);
Console.WriteLine($"Page views: {views}");

// With cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try 
{
    await cache.IncrementAsync("api:calls", cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Increment operation cancelled");
}
```

**Using Connection String Configuration:**

```csharp
using Service.Configuration;
using Service.Providers;

// Redis with connection string
var redisOptions = new RedisOptions
{
    ConnectionString = "localhost:6379", // Alternative to Endpoint/Port
    UseSsl = false,
    Database = 0
};

var redisCacheOptions = new CacheOptions { Redis = redisOptions };
var redisProvider = new RedisCacheProvider(redisCacheOptions);
var redisCache = redisProvider.CreateCache();

// SQL Server with connection string
var sqlOptions = new SqlServerOptions
{
    ConnectionString = "Server=prod-sql.company.com;Database=ProdCache;User Id=cache_user;Password=secure_password;Encrypt=true;",
    TableName = "AppCache",
    SchemaName = "cache"
};

var sqlCacheOptions = new CacheOptions { SqlServer = sqlOptions };
var sqlProvider = new SqlServerCacheProvider(sqlCacheOptions);
var sqlCache = sqlProvider.CreateCache();
```

#### Manual Configuration (Without appsettings.json)

```csharp
// Option 1: Use configuration action
services.AddCacheService(options =>
{
    options.Redis.Endpoint = "localhost";
    options.Redis.Port = 6379;
    options.Redis.UseSsl = false;
    options.Redis.Retry.MaxRetries = 3;
    options.Redis.Retry.DelaySeconds = 2;
});

// Option 2: Use configuration object
var cacheOptions = new CacheOptions
{
    Redis = new RedisOptions
    {
        ConnectionString = "localhost:6379", // Alternative to Endpoint/Port
        UseSsl = false,
        Retry = new RetryOptions
        {
            MaxRetries = 5,
            DelaySeconds = 1,
            Enabled = true
        }
    }
};
services.AddCacheService(cacheOptions);

// Option 3: SQL Server configuration action
services.AddCacheService(options =>
{
    options.DefaultProvider = "SqlServer";
    options.SqlServer.ConnectionString = "Server=localhost;Database=CacheDB;Trusted_Connection=true;";
    options.SqlServer.TableName = "MyCache";
    options.SqlServer.SchemaName = "dbo";
    options.SqlServer.CleanupIntervalMinutes = 10;
    options.SqlServer.Retry.MaxRetries = 3;
    options.SqlServer.Retry.DelaySeconds = 2;
});

// Option 4: SQL Server configuration object
var sqlCacheOptions = new CacheOptions
{
    DefaultProvider = "SqlServer",
    SqlServer = new SqlServerOptions
    {
        ConnectionString = "Server=prod-sql.company.com;Database=ProdCache;User Id=cache_user;Password=secure_password;",
        TableName = "ProductionCache",
        SchemaName = "cache",
        CleanupIntervalMinutes = 5,
        Retry = new RetryOptions
        {
            MaxRetries = 5,
            DelaySeconds = 1,
            Enabled = true
        }
    }
};
services.AddCacheService(sqlCacheOptions);
```

## SQL Server Cache Setup

**IMPORTANT: The SQL Server cache provider requires manual table creation.** You must create the cache table before using the provider. If the table doesn't exist, the provider will throw an `InvalidOperationException` with the required table creation script.

**Required Table Structure:**

```sql
-- Create cache table for SQL Server Cache Provider
CREATE TABLE [dbo].[CacheItems] (
    [Key] NVARCHAR(900) NOT NULL PRIMARY KEY,
    [Value] NVARCHAR(MAX) NULL,
    [ExpiresAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create index for efficient cleanup of expired items
CREATE INDEX IX_CacheItems_ExpiresAt ON [dbo].[CacheItems] ([ExpiresAt]);
```

**Database Requirements:**
- SQL Server 2012 or later
- Database must exist (not created automatically)
- Cache table must be created manually using the script above
- User must have CRUD permissions on the cache table
- Recommended: Separate database or schema for cache tables

**Performance Considerations:**
- Enable automatic cleanup by setting `CleanupIntervalMinutes` (default: 15 minutes)
- Consider table partitioning for high-volume scenarios
- Monitor index fragmentation on the ExpiresAt column

## Cancellation Token Support

All async operations in the cache service support `CancellationToken` for cooperative cancellation. This enables timeouts, request cancellation, and graceful shutdown scenarios.

### Basic Timeout Example

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try 
{
    var value = await cache.GetAsync("key", cts.Token);
    Console.WriteLine($"Retrieved: {value}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation timed out after 5 seconds");
}
```

### ASP.NET Core Request Cancellation

```csharp
[HttpPost("set")]
public async Task<IActionResult> SetValue(string key, string value, CancellationToken cancellationToken)
{
    try 
    {
        // Automatically cancels if user closes browser or request times out
        var success = await _cache.SetAsync(key, value, 3600, cancellationToken: cancellationToken);
        return success ? Ok() : BadRequest();
    }
    catch (OperationCanceledException)
    {
        return StatusCode(499); // Client closed connection
    }
}

[HttpGet("get/{key}")]
public async Task<IActionResult> GetValue(string key, CancellationToken cancellationToken)
{
    try 
    {
        var value = await _cache.GetAsync(key, cancellationToken);
        return value != null ? Ok(value) : NotFound();
    }
    catch (OperationCanceledException)
    {
        return StatusCode(499);
    }
}
```

### Background Service with Graceful Shutdown

```csharp
public class CacheWarmupService : BackgroundService
{
    private readonly ICache _cache;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try 
            {
                await WarmupCache(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is shutting down gracefully
                break;
            }
        }
    }
    
    private async Task WarmupCache(CancellationToken cancellationToken)
    {
        // Each cache operation respects cancellation during shutdown
        await _cache.SetAsync("warmup:timestamp", DateTime.UtcNow.ToString(), 3600, cancellationToken: cancellationToken);
        await _cache.IncrementAsync("warmup:count", cancellationToken);
    }
}
```

### Manual Cancellation

```csharp
using var cts = new CancellationTokenSource();

// Start a cache operation
var task = cache.GetAsync("expensive-key", cts.Token);

// Cancel after some condition
if (userRequestedCancel)
{
    cts.Cancel();
}

try 
{
    var result = await task;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cache operation was cancelled");
}
```

### Benefits of Cancellation Tokens

- **Resource Management**: Connections and operations are cleaned up immediately when cancelled
- **Timeout Support**: Operations can be cancelled after a specified timeout
- **ASP.NET Core Integration**: Web requests automatically cancel ongoing cache operations when clients disconnect
- **Graceful Shutdown**: Background services can cancel cache operations during application shutdown
- **Performance**: Prevents unnecessary work when operations are no longer needed

## Note about Keys
You do not have to create a key and then add a value to it.  If a key does not exist, it will be created for you and the value you set will be applied to it.  This is true for all types of keys.  There is an override on "setting" methods that let you not create a non-existing key or not overwrite an existing key.

## Sets

Sets are unordered, unique collections of strings. Duplicate values are automatically ignored.

**⚠️ Note: Sets are only supported by the Redis provider. SQL Server provider will throw `NotSupportedException`.**

```csharp
// AddToSet with 1 hour expiration (Redis only)
await cache.AddToSetAsync("your-key", "your unique value1", 3600);
await cache.AddToSetAsync("your-key", "your unique value2", 3600);

// Return your entire set stored at this key (Redis only)
var yourset = await cache.GetSetAsync("your-key");
```

## Strings

Strings store a single string value per key.

**✅ Supported by both Redis and SQL Server providers.**

```csharp
// Set string with 1 hour expiration
await cache.SetAsync("your-key", "your value", 3600);

// Set only if key doesn't exist (works with both providers)
bool wasSet = await cache.SetIfNotExistsAsync("your-key", "new value", 3600);

// Get string value
var yourstring = await cache.GetAsync("your-key");

// All operations support cancellation tokens
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await cache.SetAsync("your-key", "your value", 3600, cancellationToken: cts.Token);
var value = await cache.GetAsync("your-key", cts.Token);
```

## Lists

Lists are ordered collections that allow duplicate values. Each "add" operation appends to the end.

**⚠️ Note: Lists are only supported by the Redis provider. SQL Server provider will throw `NotSupportedException`.**

```csharp
// AddToList with 1 hour expiration (Redis only)
await cache.AddToListAsync("your-key", "your value1", 3600);
await cache.AddToListAsync("your-key", "your value2", 3600);

// Return your entire list stored at this key (Redis only)
var yourlist = await cache.GetListAsync("your-key");
```


## Hashes

Hashes store field-value pairs. Each field name is unique within a hash.

**⚠️ Note: Hashes are only supported by the Redis provider. SQL Server provider will throw `NotSupportedException`.**

```csharp
// AddToHash with 1 hour expiration (Redis only)
await cache.AddToHashAsync("your-key", "unique-field1", "your value1", 3600);
await cache.AddToHashAsync("your-key", "unique-field2", "your value2", 3600);

// Get specific hash field value (Redis only)
var yourhashvalue = await cache.GetHashAsync("your-key", "unique-field1");

// Get entire hash as dictionary (Redis only)
var yourHashDictionary = await cache.GetHashAllAsync("your-key");
```

## Counters

Counters provide atomic increment and decrement operations.

**✅ Supported by both Redis and SQL Server providers.**

```csharp
// Increment by 1 (default) - works with both providers
await cache.IncrementAsync("your-key");

// Increment by specific amount - works with both providers
await cache.IncrementByAsync("your-key", 5);

// Decrement by 1 (default) - works with both providers
await cache.DecrementAsync("your-key");

// Decrement by specific amount - works with both providers
await cache.DecrementByAsync("your-key", 5);

// All counter operations support cancellation tokens
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await cache.IncrementAsync("your-key", cts.Token);
await cache.DecrementByAsync("your-key", 3, cts.Token);
```

## Key Deletion

**✅ Supported by both Redis and SQL Server providers.**

```csharp
// Delete a single key - works with both providers
await cache.DeleteAsync("your-key");

// Delete multiple keys - works with both providers
await cache.DeleteAsync(new[] { "key1", "key2", "key3" });

// Delete operations support cancellation tokens
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await cache.DeleteAsync("your-key", cts.Token);
await cache.DeleteAsync(new[] { "key1", "key2", "key3" }, cts.Token);
```

