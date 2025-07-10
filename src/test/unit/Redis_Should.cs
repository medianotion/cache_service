using Xunit;
using Moq;
using Service;
using StackExchange.Redis;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace unit
{
    public class Redis_Should
    {

        [Fact]
        public void SetupRetryRetriesZero()
        {
            Assert.Throws<ArgumentException>(() => CacheUtilities.SetupRedisRetryPolicy(0,1));
        }

        [Fact]
        public void SetupRetryRetriesLessThanZero()
        {
            Assert.Throws<ArgumentException>(() => CacheUtilities.SetupRedisRetryPolicy(-1, 1));
        }

        [Fact]
        public void SetupRetryDelayLessThanZero()
        {
            Assert.Throws<ArgumentException>(() => CacheUtilities.SetupRedisRetryPolicy(1, -1));
        }

        [Fact]
        public void SetupRetryDelayZero()
        {
            Assert.Throws<ArgumentException>(() => CacheUtilities.SetupRedisRetryPolicy(1, 0));
        }


        [Fact]
        public void ValidateKeyNull()
        {
            Assert.Throws<ArgumentNullException>(() => CacheUtilities.ValidateKey(null));
        }
        [Fact]
        public void ValidateKeyValueNull()
        {
            string key = null;
            Assert.Throws<ArgumentNullException>(() => CacheUtilities.ValidateKey(key));
        }

        [Fact]
        public void ValidateKeyValueEmpty()
        {
            var key = new string("");
            Assert.Throws<ArgumentNullException>(() => CacheUtilities.ValidateKey(null));
        }

        [Fact]
        public void ValidateExpirationZero()
        {
            Assert.Throws<ArgumentException>(() => CacheUtilities.ValidateExpiration(0));
        }

        [Fact]
        public void ValidateExpirationLessThanZero()
        {
            Assert.Throws<ArgumentException>(() => CacheUtilities.ValidateExpiration(-1));
        }




        [Fact]
        public async Task IncrementAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 1;
            db.Setup(x => x.StringIncrementAsync(new RedisKey(key), expectedResult, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.IncrementAsync(key);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task IncrementByAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 3;
            db.Setup(x => x.StringIncrementAsync(new RedisKey(key), expectedResult, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.IncrementByAsync(key,expectedResult);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task DecrementAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 1;
            db.Setup(x => x.StringDecrementAsync(new RedisKey(key), expectedResult, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.DecrementAsync(key);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task DecrementByAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 3;
            db.Setup(x => x.StringDecrementAsync(new RedisKey(key), expectedResult, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.DecrementByAsync(key, expectedResult);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task DeleteAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = true;
            db.Setup(x => x.KeyDeleteAsync(new RedisKey(key),CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.DeleteAsync(key);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task DeleteManyAsync()
        {
            bool expectedResult = true;
            RedisKey[] keys = { "key", "key2", "key3" };
            var db = new Mock<IDatabase>();

            db.Setup(x => x.KeyDeleteAsync(keys, CommandFlags.None)).Returns(Task.FromResult((long)keys.Length));
            var redis = new Redis(db.Object);
            var result = await redis.DeleteAsync(new string[] { keys[0].ToString(), keys[1].ToString(), keys[2].ToString() });
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task SetAsync()
        {

            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = true;
            string Value = "value";
            int ttlSeconds = 1;
            var ttl = new TimeSpan(0, 0, ttlSeconds);
            db.Setup(x => x.StringSetAsync(new RedisKey(key), Value, ttl, When.Always)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.SetAsync(key, Value, ttlSeconds);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task SetAsyncNoCreateOrOverwrite()
        {

            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = false;
            string Value = "value";
            int ttlSeconds = 1;
            var ttl = new TimeSpan(0, 0, ttlSeconds);
            db.Setup(x => x.StringSetAsync(new RedisKey(key), Value, ttl, When.Always, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.SetAsync(key, Value, ttlSeconds,false);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task GetAsync()
        {

            var key = "key";
            var db = new Mock<IDatabase>();
            string expectedResult = "value";
            db.Setup(x => x.StringGetAsync(new RedisKey(key), CommandFlags.None)).Returns(Task.FromResult(new RedisValue(expectedResult)));
            var redis = new Redis(db.Object);
            var result = await redis.GetAsync(key);
            Assert.Equal(expectedResult, result);

        }

        [Fact]
        public async Task GetAsyncNoKeyReturnsNull()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string expectedResult = null;
            var redis = new Redis(db.Object);
            var result = await redis.GetAsync(key);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task AddToListAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 1;
            int expireInSeconds = 1;
            bool Expire = true;
            string Value = "value";
            db.Setup(x => x.ListRightPushAsync(new RedisKey(key), Value, When.Always, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            db.Setup(x => x.KeyExpireAsync(new RedisKey(key), new TimeSpan(0, 0, expireInSeconds), CommandFlags.None)).Returns(Task.FromResult(Expire));
            var redis = new Redis(db.Object);
            var result = await redis.AddToListAsync(key, Value, expireInSeconds);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task AddToListAsyncNoCreateOrOverwrite()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 0;
            int expireInSeconds = 1;
            bool Expire = true;
            string Value = "value";
            db.Setup(x => x.ListRightPushAsync(new RedisKey(key), Value, When.Always, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            db.Setup(x => x.KeyExpireAsync(new RedisKey(key), new TimeSpan(0, 0, expireInSeconds), CommandFlags.None)).Returns(Task.FromResult(Expire));
            var redis = new Redis(db.Object);
            var result = await redis.AddToListAsync(key, Value, expireInSeconds, false);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task GetListAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string Value = "value";
            var array = new RedisValue[] { Value, Value, Value };
            var expectedResult = new List<string>(array.ToStringArray());
            db.Setup(x => x.ListRangeAsync(new RedisKey(key),0,-1, CommandFlags.None)).Returns(Task.FromResult(array));
            var redis = new Redis(db.Object);
            var result = await redis.GetListAsync(key);
            Assert.Equal(expectedResult, result);
        }
        [Fact]
        public async Task RemoveFromListAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string Value = "value";
            long expectedResult = 0;
            db.Setup(x => x.ListRemoveAsync(new RedisKey(key), Value, 0, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.RemoveFromListAsync(key,Value);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task LengthOfListAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 0;
            db.Setup(x => x.ListLengthAsync(new RedisKey(key), CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.LengthOfListAsync(key);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task GetSetAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string Value = "value";
            var array = new RedisValue[] { Value, Value, Value };
            var expectedResult = new List<string>(array.ToStringArray());
            db.Setup(x => x.SetMembersAsync(new RedisKey(key), CommandFlags.None)).Returns(Task.FromResult(array));
            var redis = new Redis(db.Object);
            var result = await redis.GetSetAsync(key);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task AddToSetAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = true;
            string Value = "value";
            int expireInSeconds = 1;
            db.Setup(x => x.SetAddAsync(new RedisKey(key), Value, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            db.Setup(x => x.KeyExpireAsync(new RedisKey(key), new TimeSpan(0, 0, expireInSeconds), CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.AddToSetAsync(key, Value, expireInSeconds);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task RemoveFromSetAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string Value = "value";
            bool expectedResult = true;
            db.Setup(x => x.SetRemoveAsync(new RedisKey(key), Value, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.RemoveFromSetAsync(key, Value);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task LengthOfSetAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 0;
            db.Setup(x => x.SetLengthAsync(new RedisKey(key), CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.LengthOfSetAsync(key);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task AddToHashAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = true;
            int expireInSeconds = 1;
            
            db.Setup(x => x.HashSetAsync(new RedisKey(key), new RedisValue("field"), new RedisValue("value"),When.Always, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            db.Setup(x => x.KeyExpireAsync(new RedisKey(key), new TimeSpan(0, 0, expireInSeconds), CommandFlags.None)).Returns(Task.FromResult(expectedResult));

            var redis = new Redis(db.Object);
            await redis.AddToHashAsync(key, "field", "value", expireInSeconds);
            Assert.True(true);
        }

        [Fact]
        public async Task GetHashAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string expectedResult = "value";
            db.Setup(x => x.HashGetAsync(new RedisKey(key), new RedisValue("field"), CommandFlags.None)).Returns(Task.FromResult(new RedisValue(expectedResult)));
            var redis = new Redis(db.Object);
            await redis.GetHashAsync(key, "field");
            Assert.True(true);
        }

        [Fact]
        public async Task GetHashAllAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string value = "value";
            List<HashEntry> hash = new List<HashEntry>();
            hash.Add(new HashEntry(key, value));
            var expectedResult = new Dictionary<string, string> { { key, value } };
            db.Setup(x => x.HashGetAllAsync(new RedisKey(key), CommandFlags.None)).Returns(Task.FromResult(hash.ToArray()));
            var redis = new Redis(db.Object);
            var result = await redis.GetHashAllAsync(key);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task RemoveFromHashAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            string Value = "value";
            bool expectedResult = true;
            db.Setup(x => x.HashDeleteAsync(new RedisKey(key), Value, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.RemoveFromHashAsync(key, Value);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task LengthOfHashAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            long expectedResult = 0;
            db.Setup(x => x.HashLengthAsync(new RedisKey(key), CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.LengthOfHashAsync(key);
            Assert.Equal(expectedResult, result);
        }


        [Fact]
        public async Task ExpireKeyAsync()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = true;
            int expireInSeconds = 1;

            db.Setup(x => x.KeyExpireAsync(new RedisKey(key), new TimeSpan(0,0,1), CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            var result = await redis.ExpireKey(key, expireInSeconds);
            Assert.True(result);
        }
        [Fact]
        public async Task ExpireKeyAsyncKeyExistsDoNotSetTTL()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = false;
            string Value = "value";
            int expireInSeconds = 1;
            var ttl = new TimeSpan(0, 0, expireInSeconds);
            db.Setup(x => x.StringSetAsync(new RedisKey(key), Value, ttl, When.Always, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            await redis.SetAsync(key, Value, expireInSeconds);
            var result = await redis.ExpireKey(key, expireInSeconds);
            Assert.True(result);
        }
        [Fact]
        public async Task ExpireKeyAsyncKeyExistTTLChanged()
        {
            var key = "key";
            var db = new Mock<IDatabase>();
            bool expectedResult = true;
            string Value = "value";
            int expireInSeconds = 1;
            int updateExpireInSeconds = 2;
            var ttl = new TimeSpan(0, 0, expireInSeconds);
            db.Setup(x => x.StringSetAsync(new RedisKey(key), Value, ttl, When.Always, CommandFlags.None)).Returns(Task.FromResult(expectedResult));
            var redis = new Redis(db.Object);
            await redis.SetAsync(key, Value, expireInSeconds);
            var result = await redis.ExpireKey(key, updateExpireInSeconds);
            Assert.True(result);
        }

        [Fact]
        public void LogRetryException()
        {
            CacheUtilities.LogRetryException(new Exception(), 1, new Polly.Context());
            Assert.True(true);
        }
        [Fact]
        public void LogRetryExceptionNullParams()
        {
            CacheUtilities.LogRetryException(null, 1, null);
            Assert.True(true);
        }

    }
}
