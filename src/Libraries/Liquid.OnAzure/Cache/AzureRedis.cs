using Liquid.Base;
using Liquid.Runtime;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Redis;
using System;
using System.Threading.Tasks;

namespace Liquid.OnAzure
{
    /// <summary>
    ///  Include support of AzureRedis, that processing data included on Configuration file.
    /// </summary>
    public sealed class AzureRedis : LightCache, IDisposable
    {
        private AzureRedisConfiguration config;
        private RedisCache _redisClient;
        private DistributedCacheEntryOptions _options;

        /// <inheritdoc/>
        public override void Initialize()
        {
            config = LightConfigurator.LoadConfig<AzureRedisConfiguration>("AzureRedis");
            _redisClient = new(new RedisCacheOptions()
            {
                Configuration = config.Configuration,
                InstanceName = config.InstanceName
            });

            _options = new()
            {
                SlidingExpiration = TimeSpan.FromSeconds(config.SlidingExpirationSeconds),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(config.AbsoluteExpirationRelativeToNowSeconds)
            };
        }

        /// <inheritdoc/>
        public override T Get<T>(string key)
        {
            var data = _redisClient.Get(key);
            return FromByteArray<T>(data);
        }

        /// <inheritdoc/>
        public override async Task<T> GetAsync<T>(string key)
        {
            var data = await _redisClient.GetAsync(key);
            return FromByteArray<T>(data);
        }

        /// <inheritdoc/>
        public override void Refresh(string key)
        {
            _redisClient.Refresh(key);
        }

        /// <inheritdoc/>
        public override async Task RefreshAsync(string key)
        {
            await _redisClient.RefreshAsync(key);
        }

        /// <inheritdoc/>
        public override void Remove(string key)
        {
            _redisClient.Remove(key);
        }

        /// <inheritdoc/>
        public override Task RemoveAsync(string key)
        {
            return _redisClient.RemoveAsync(key);
        }

        /// <inheritdoc/>
        public override void Set<T>(string key, T value)
        {
            _redisClient.Set(key, ToByteArray(value), _options);
        }

        /// <inheritdoc/>
        public override async Task SetAsync<T>(string key, T value)
        {
            await _redisClient.SetAsync(key, ToByteArray(value), _options);
        }

        /// <inheritdoc/>
        public override LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value)
        {
            try
            {
                var redis = _redisClient.GetAsync(serviceKey);
                return LightHealth.HealthCheckStatus.Healthy;
            }
            catch
            {
                return LightHealth.HealthCheckStatus.Unhealthy;
            }
        }

        /// <summary>
        /// Disposes memory resources
        /// </summary>
        public void Dispose()
        {
            _redisClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}