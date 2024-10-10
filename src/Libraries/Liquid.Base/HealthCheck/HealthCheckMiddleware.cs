using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Liquid.Base
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class HealthCheckExtension
    {
        /// <summary>
        /// Enables a mock middleware for a non Production environments
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseHealthCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<HealthCheckMiddleware>();
        }
    }

    /// <summary>
    /// Mock Middleware
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="next"></param>
    public class HealthCheckMiddleware(RequestDelegate next)
    {

        /// <summary>
        /// Middleware invoke process
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            if (context is null)
                return;

            if (context.Request.Path.Value.Equals("/health", System.StringComparison.CurrentCultureIgnoreCase))
            {
                LightHealthResult healthResult = new() { Status = LightHealth.HealthCheckStatus.Healthy.ToString() };

                context.Response.StatusCode = 200; // Success
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                string jsonString = healthResult.ToJsonString();
                await context.Response.WriteAsync(jsonString);
                return;
            }

            await next.Invoke(context);
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}