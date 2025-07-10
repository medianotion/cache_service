using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Polly;
using System.Linq;
using Polly.Retry;
using System.Diagnostics.CodeAnalysis;
using Service.Configuration;
using System.Threading;
using System.Data;
using System.Text.Json;

namespace Service
{
    internal class SqlServer : ICache
    {
        private const int DefaultRetries = 3;
        private const int RetryDelaySeconds = 2;

        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _schemaName;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly Timer _cleanupTimer;

        [ExcludeFromCodeCoverage]
        public SqlServer(SqlServerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrEmpty(options.ConnectionString))
                throw new ArgumentException("ConnectionString is required", nameof(options));

            _connectionString = options.ConnectionString;
            _tableName = options.TableName;
            _schemaName = options.SchemaName;

            var retries = options.Retry.Enabled ? options.Retry.MaxRetries : DefaultRetries;
            var delaySeconds = options.Retry.DelaySeconds > 0 ? options.Retry.DelaySeconds : RetryDelaySeconds;
            _retryPolicy = CacheUtilities.SetupSqlServerRetryPolicy(retries, delaySeconds);

            // Verify table exists - throw error with script if not
            VerifyTableExistsAsync().GetAwaiter().GetResult();

            // Cleanup expired items periodically
            var cleanupInterval = TimeSpan.FromMinutes(options.CleanupIntervalMinutes);
            _cleanupTimer = new Timer(async _ => await CleanupExpiredItemsAsync(), null, cleanupInterval, cleanupInterval);
        }

        // used for unit testing
        [ExcludeFromCodeCoverage]
        internal SqlServer(string connectionString, string tableName = "CacheItems", string schemaName = "dbo")
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _tableName = tableName;
            _schemaName = schemaName;
            _retryPolicy = CacheUtilities.SetupSqlServerRetryPolicy(DefaultRetries, RetryDelaySeconds);
        }

        private async Task VerifyTableExistsAsync()
        {
            var checkTableSql = $@"
                SELECT COUNT(*) 
                FROM sys.tables 
                WHERE name = '{_tableName}' AND schema_id = SCHEMA_ID('{_schemaName}')";

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    using var command = new SqlCommand(checkTableSql, connection);
                    var tableExists = (int)await command.ExecuteScalarAsync() > 0;
                    
                    if (!tableExists)
                    {
                        var createTableScript = GetCreateTableScript();
                        throw new InvalidOperationException($"Required cache table [{_schemaName}].[{_tableName}] does not exist. Please create the table using the following script:\n\n{createTableScript}");
                    }
                });
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                var createTableScript = GetCreateTableScript();
                throw new InvalidOperationException($"Unable to verify cache table [{_schemaName}].[{_tableName}] exists. Please ensure the table exists using the following script:\n\n{createTableScript}\n\nOriginal error: {ex.Message}", ex);
            }
        }

        private string GetCreateTableScript()
        {
            return $@"
-- Create cache table for SQL Server Cache Provider
CREATE TABLE [{_schemaName}].[{_tableName}] (
    [Key] NVARCHAR(900) NOT NULL PRIMARY KEY,
    [Value] NVARCHAR(MAX) NULL,
    [ExpiresAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create index for efficient cleanup of expired items
CREATE INDEX IX_{_tableName}_ExpiresAt ON [{_schemaName}].[{_tableName}] ([ExpiresAt]);";
        }

        private async Task CleanupExpiredItemsAsync()
        {
            var deleteSql = $@"
                DELETE FROM [{_schemaName}].[{_tableName}] 
                WHERE [ExpiresAt] < GETUTCDATE()";

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    using var command = new SqlCommand(deleteSql, connection);
                    await command.ExecuteNonQueryAsync();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }



        public async Task<string> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                var selectSql = $@"
                    SELECT [Value] 
                    FROM [{_schemaName}].[{_tableName}] 
                    WHERE [Key] = @Key AND [ExpiresAt] > GETUTCDATE()";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand(selectSql, connection);
                command.Parameters.AddWithValue("@Key", key);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result?.ToString();
            });
        }

        public async Task<bool> SetAsync(string key, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                CacheUtilities.ValidateExpiration(expireInSeconds);

                var expiresAt = DateTime.UtcNow.AddSeconds(expireInSeconds);

                string sql;
                if (create_or_overwrite)
                {
                    sql = $@"
                        MERGE [{_schemaName}].[{_tableName}] AS target
                        USING (SELECT @Key AS [Key], @Value AS [Value], @ExpiresAt AS [ExpiresAt]) AS source
                        ON target.[Key] = source.[Key]
                        WHEN MATCHED THEN
                            UPDATE SET [Value] = source.[Value], [ExpiresAt] = source.[ExpiresAt]
                        WHEN NOT MATCHED THEN
                            INSERT ([Key], [Value], [ExpiresAt]) VALUES (source.[Key], source.[Value], source.[ExpiresAt]);";
                }
                else
                {
                    sql = $@"
                        INSERT INTO [{_schemaName}].[{_tableName}] ([Key], [Value], [ExpiresAt])
                        SELECT @Key, @Value, @ExpiresAt
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [{_schemaName}].[{_tableName}] 
                            WHERE [Key] = @Key AND [ExpiresAt] > GETUTCDATE()
                        )";
                }

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Key", key);
                command.Parameters.AddWithValue("@Value", value ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ExpiresAt", expiresAt);

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                return rowsAffected > 0;
            });
        }

        public async Task<bool> SetIfNotExistsAsync(string key, string value, int expireInSeconds, CancellationToken cancellationToken = default)
        {
            return await SetAsync(key, value, expireInSeconds, false, cancellationToken);
        }

        public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                var deleteSql = $@"
                    DELETE FROM [{_schemaName}].[{_tableName}] 
                    WHERE [Key] = @Key";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand(deleteSql, connection);
                command.Parameters.AddWithValue("@Key", key);

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                return rowsAffected > 0;
            });
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                foreach (string key in keys)
                {
                    CacheUtilities.ValidateKey(key);
                }

                var keyList = keys.ToList();
                var parameters = string.Join(",", keyList.Select((_, i) => $"@Key{i}"));
                
                var deleteSql = $@"
                    DELETE FROM [{_schemaName}].[{_tableName}] 
                    WHERE [Key] IN ({parameters})";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand(deleteSql, connection);
                
                for (int i = 0; i < keyList.Count; i++)
                {
                    command.Parameters.AddWithValue($"@Key{i}", keyList[i]);
                }

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                return rowsAffected == keys.Count();
            });
        }

        public async Task<bool> ExpireKey(string key, int expireInSeconds, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);
                CacheUtilities.ValidateExpiration(expireInSeconds);

                var expiresAt = DateTime.UtcNow.AddSeconds(expireInSeconds);

                var updateSql = $@"
                    UPDATE [{_schemaName}].[{_tableName}] 
                    SET [ExpiresAt] = @ExpiresAt
                    WHERE [Key] = @Key AND [ExpiresAt] > GETUTCDATE()";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand(updateSql, connection);
                command.Parameters.AddWithValue("@Key", key);
                command.Parameters.AddWithValue("@ExpiresAt", expiresAt);

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                return rowsAffected > 0;
            });
        }

        public async Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default)
        {
            return await IncrementByAsync(key, 1, cancellationToken);
        }

        public async Task<long> IncrementByAsync(string key, long number, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                CacheUtilities.ValidateKey(key);

                var sql = $@"
                    DECLARE @CurrentValue BIGINT = 0;
                    DECLARE @NewValue BIGINT;
                    DECLARE @ExpiresAt DATETIME2;
                    
                    SELECT @CurrentValue = CAST([Value] AS BIGINT), @ExpiresAt = [ExpiresAt]
                    FROM [{_schemaName}].[{_tableName}] 
                    WHERE [Key] = @Key AND [ExpiresAt] > GETUTCDATE();
                    
                    SET @NewValue = @CurrentValue + @Increment;
                    SET @ExpiresAt = ISNULL(@ExpiresAt, DATEADD(HOUR, 1, GETUTCDATE()));
                    
                    MERGE [{_schemaName}].[{_tableName}] AS target
                    USING (SELECT @Key AS [Key], CAST(@NewValue AS NVARCHAR(MAX)) AS [Value], @ExpiresAt AS [ExpiresAt]) AS source
                    ON target.[Key] = source.[Key]
                    WHEN MATCHED THEN
                        UPDATE SET [Value] = source.[Value], [ExpiresAt] = source.[ExpiresAt]
                    WHEN NOT MATCHED THEN
                        INSERT ([Key], [Value], [ExpiresAt]) VALUES (source.[Key], source.[Value], source.[ExpiresAt]);
                    
                    SELECT @NewValue;";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Key", key);
                command.Parameters.AddWithValue("@Increment", number);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt64(result);
            });
        }

        public async Task<long> DecrementAsync(string key, CancellationToken cancellationToken = default)
        {
            return await IncrementByAsync(key, -1, cancellationToken);
        }

        public async Task<long> DecrementByAsync(string key, long number, CancellationToken cancellationToken = default)
        {
            return await IncrementByAsync(key, -number, cancellationToken);
        }

        // SQL Server doesn't support native lists - these operations will throw NotSupportedException
        public Task<long> LengthOfListAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Lists are not supported by SqlServer provider. Use Redis provider for list operations.");
        }

        public Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Lists are not supported by SqlServer provider. Use Redis provider for list operations.");
        }

        public Task<long> AddToListAsync(string key, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Lists are not supported by SqlServer provider. Use Redis provider for list operations.");
        }

        public Task<long> RemoveFromListAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Lists are not supported by SqlServer provider. Use Redis provider for list operations.");
        }

        // SQL Server doesn't support native sets - these operations will throw NotSupportedException
        public Task<bool> AddToSetAsync(string key, string value, int expireInSeconds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Sets are not supported by SqlServer provider. Use Redis provider for set operations.");
        }

        public Task<List<string>> GetSetAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Sets are not supported by SqlServer provider. Use Redis provider for set operations.");
        }

        public Task<long> LengthOfSetAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Sets are not supported by SqlServer provider. Use Redis provider for set operations.");
        }

        public Task<bool> RemoveFromSetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Sets are not supported by SqlServer provider. Use Redis provider for set operations.");
        }

        // SQL Server doesn't support native hashes - these operations will throw NotSupportedException
        public Task<bool> AddToHashAsync(string key, string field, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Hashes are not supported by SqlServer provider. Use Redis provider for hash operations.");
        }

        public Task<string> GetHashAsync(string key, string field, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Hashes are not supported by SqlServer provider. Use Redis provider for hash operations.");
        }

        public Task<Dictionary<string, string>> GetHashAllAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Hashes are not supported by SqlServer provider. Use Redis provider for hash operations.");
        }

        public Task<long> LengthOfHashAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Hashes are not supported by SqlServer provider. Use Redis provider for hash operations.");
        }

        public Task<bool> RemoveFromHashAsync(string key, string fieldKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Hashes are not supported by SqlServer provider. Use Redis provider for hash operations.");
        }


        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}