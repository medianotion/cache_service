# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is Cache Service, a .NET cache service library that provides a Redis implementation with built-in reliability features. The library wraps StackExchange.Redis and adds Polly retry policies for better error handling.

## Architecture

- **Main Library**: `src/cache_service/` - Contains the core cache implementation
  - `ICache.cs` - Interface defining all cache operations (strings, lists, sets, hashes, counters)
  - `Redis.cs` - Main Redis implementation with Polly retry policies
  - `CacheFactory.cs` - Factory class for creating cache instances
- **Tests**: `src/test/unit/` - xUnit tests with Moq for mocking
- **Target Framework**: .NET Standard 2.1 (main library), .NET 8.0 (tests)

## Key Dependencies

- **StackExchange.Redis** (2.2.79) - Redis client
- **Polly** (7.2.2) - Retry and resilience policies
- **xUnit** - Testing framework
- **Moq** - Mocking framework for tests
- **AutoFixture.Xunit2** - Test data generation
- **coverlet** - Code coverage analysis

## Development Commands

### Build
```bash
dotnet build cache-service.sln
```

### Run Tests
```bash
dotnet test src/test/unit/unittest.csproj
```

### Run Tests with Coverage
```bash
dotnet test src/test/unit/unittest.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Package Generation
The main project is configured to generate NuGet packages manually (GeneratePackageOnBuild=false).

## Cache Operations Support

The library supports all major Redis data types:
- **Strings** - Single string values per key
- **Lists** - Ordered, non-unique collections
- **Sets** - Unordered, unique collections  
- **Hashes** - Key-value field collections
- **Counters** - Increment/decrement operations

All "setting" operations require an `expireInSeconds` parameter for automatic TTL management.

## Usage Pattern

```csharp
var cache = CacheFactory.CreateRedis("redis-endpoint", useSSL: false);
await cache.SetStringAsync("key", "value", 3600); // 1 hour expiration
```

## Testing Approach

- Unit tests focus on the Redis class static methods and error conditions
- Tests use Moq for mocking Redis dependencies
- AutoFixture provides test data generation
- Tests verify retry policy setup and argument validation

## Key Extension Features

- **RedisKeyEnumExtensions** - Provides enum-to-key string conversion with prefixes
- **Automatic Expiration** - All set operations include TTL management
- **Retry Policies** - Built-in resilience for Redis server exceptions and timeouts