using Liquid.Base;
using Liquid.Interfaces;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Liquid.Runtime
{
    /// <summary>
    /// Include support of Cache, that processing data included on Configuration file.
    /// </summary>
    public abstract class LightCache : ILightCache
    {
        /// <inheritdoc/>
        public abstract void Initialize();
        /// <inheritdoc/>
        public abstract T Get<T>(string key);
        /// <inheritdoc/>
        public abstract Task<T> GetAsync<T>(string key);
        /// <inheritdoc/>
        public abstract void Set<T>(string key, T value);
        /// <inheritdoc/>
        public abstract Task SetAsync<T>(string key, T value);
        /// <inheritdoc/>
        public abstract void Refresh(string key);
        /// <inheritdoc/>
        public abstract Task RefreshAsync(string key);
        /// <inheritdoc/>
        public abstract void Remove(string key);
        /// <inheritdoc/>
        public abstract Task RemoveAsync(string key);

        /// <inheritdoc/>
        public static byte[] ToByteArray(object anyObj)
        {
#pragma warning disable IDE0063 // Use simple 'using' statement
            using (var m = new MemoryStream())
            {
                var ser = new DataContractSerializer(anyObj?.GetType());
                ser.WriteObject(m, anyObj);
                return m.ToArray();
            }
#pragma warning restore IDE0063 // Use simple 'using' statement
        }

        /// <inheritdoc/>
        public static T FromByteArray<T>(byte[] data)
        {
            if (data is not null)
            {
#pragma warning disable IDE0063 // Use simple 'using' statement
                using (var m = new MemoryStream(data))
                {
                    var ser = new DataContractSerializer(typeof(T));
                    return (T)ser.ReadObject(m);
                }
#pragma warning restore IDE0063 // Use simple 'using' statement
            }

            return default;
        }

        /// <inheritdoc/>
        public abstract LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value);
    }
}