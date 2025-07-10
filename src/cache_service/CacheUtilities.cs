using System;
using System.Linq;
using Polly;
using Polly.Retry;

namespace Service
{
    /// <summary>
    /// Common utility methods shared across cache providers
    /// </summary>
    internal static class CacheUtilities
    {
        /// <summary>
        /// Validates that a cache key is not null or empty
        /// </summary>
        /// <param name="key">The cache key to validate</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty</exception>
        internal static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key", "key value is null or empty");
        }

        /// <summary>
        /// Validates that expiration time is greater than 0
        /// </summary>
        /// <param name="expireInSeconds">The expiration time in seconds</param>
        /// <exception cref="ArgumentException">Thrown when expireInSeconds is less than or equal to 0</exception>
        internal static void ValidateExpiration(int expireInSeconds)
        {
            if (expireInSeconds <= 0)
                throw new ArgumentException("expireInSeconds must be greater than 0", "expireInSeconds");
        }

        /// <summary>
        /// Sets up a retry policy for Redis operations
        /// </summary>
        /// <param name="retries">Number of retry attempts</param>
        /// <param name="delayInSeconds">Delay between retries in seconds</param>
        /// <returns>Configured async retry policy for Redis</returns>
        /// <exception cref="ArgumentException">Thrown when retries or delayInSeconds are invalid</exception>
        internal static AsyncRetryPolicy SetupRedisRetryPolicy(int retries, int delayInSeconds)
        {
            if (retries <= 0)
                throw new ArgumentException("retries must be greater than 0", "retries");

            if (delayInSeconds <= 0)
                throw new ArgumentException("delayInSeconds must be greater than 0", "delayInSeconds");

            return Policy
                .Handle<StackExchange.Redis.RedisServerException>()
                .Or<StackExchange.Redis.RedisTimeoutException>()
                .WaitAndRetryAsync(
                retries,
                i => new TimeSpan(0, 0, delayInSeconds),
                (exception, timeSpan, retryCount, context) => LogRetryException(exception, retryCount, context));
        }

        /// <summary>
        /// Sets up a retry policy for SQL Server operations
        /// </summary>
        /// <param name="retries">Number of retry attempts</param>
        /// <param name="delayInSeconds">Delay between retries in seconds</param>
        /// <returns>Configured async retry policy for SQL Server</returns>
        /// <exception cref="ArgumentException">Thrown when retries or delayInSeconds are invalid</exception>
        internal static AsyncRetryPolicy SetupSqlServerRetryPolicy(int retries, int delayInSeconds)
        {
            if (retries <= 0)
                throw new ArgumentException("retries must be greater than 0", "retries");

            if (delayInSeconds <= 0)
                throw new ArgumentException("delayInSeconds must be greater than 0", "delayInSeconds");

            return Policy
                .Handle<Microsoft.Data.SqlClient.SqlException>()
                .Or<InvalidOperationException>()
                .WaitAndRetryAsync(
                retries,
                i => new TimeSpan(0, 0, delayInSeconds),
                (exception, timeSpan, retryCount, context) => LogRetryException(exception, retryCount, context));
        }

        /// <summary>
        /// Logs retry exception information to console
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="retryCount">Current retry attempt number</param>
        /// <param name="context">Polly context with additional information</param>
        internal static void LogRetryException(Exception exception, int retryCount, Context context)
        {
            var action = context != null && context.Count > 0 ? context.First().Key : "unknown method";
            var actionDescription = context != null && context.Count > 0 ? context.First().Value : "unknown description";
            var message = exception != null ? exception.Message : "unknown exception message";
            var msg = $"Retry n°{retryCount} of {action} ({actionDescription}) : {message}";
            Console.WriteLine(msg);
        }
    }
}