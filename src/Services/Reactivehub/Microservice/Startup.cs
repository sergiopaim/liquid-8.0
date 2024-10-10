using Liquid;
using Liquid.Middleware;
using Liquid.OnAzure;
using Microservice.ReactiveHubs;
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
            services.AddReactiveHub(SignalRConfiguration.GetConnectionString());
        }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();

            WorkBench.UseTelemetry<AppInsights>();
            WorkBench.UseRepository<CosmosDB>();
            WorkBench.UseReactiveHub<GeneralHub>();
            WorkBench.UseWorker<ServiceBusWorker>();

            app.UseWorkBench();

            app.UseReactiveHub<GeneralHub>();
            app.UseMvc();
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}