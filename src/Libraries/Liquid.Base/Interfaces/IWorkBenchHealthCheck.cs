using Liquid.Base;

namespace Liquid.Interfaces
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public interface IWorkBenchHealthCheck : IWorkBenchService
    {
        /// <summary>
        /// Interface created to force cartridges to implement the method for it's HealthCheck
        /// </summary>
        /// <param name="serviceKey">The service key</param>
        /// <param name="value">The value to check</param>
        /// <returns></returns>
        LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value);
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}