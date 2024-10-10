using Liquid;
using Liquid.Middleware;
using Liquid.OnAzure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microservice
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class Startup(IConfiguration configuration)
    {
        //forced-deploy@v8.00.00

        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddWorkBench(Configuration);
        }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();

            WorkBench.UseTelemetry<AppInsights>();
            WorkBench.UseRepository<CosmosDB>();
            WorkBench.UseDataHub<ServiceBus>();
            WorkBench.UseWorker<ServiceBusWorker>();
            WorkBench.UseScheduler<ServiceBusScheduler>();

            app.UseWorkBench();

            app.UseMvc();
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}