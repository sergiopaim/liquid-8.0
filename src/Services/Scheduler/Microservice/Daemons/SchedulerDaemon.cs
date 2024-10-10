using Liquid;
using Liquid.Activation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Microservice.Services
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SchedulerDaemon(IServiceScopeFactory serviceScopeFactory) : LightBackgroundTask(serviceScopeFactory)
    {

        // crontab expression -> every minute
        protected override string Schedule => "* * * * *";

        public override async Task ProcessInScope(IServiceProvider serviceProvider)
        {
            try
            {
                await Factory<SchedulerService>().DispatchJobsAsync();
            }
            catch (Exception e)
            {
                Exception moreInfo = new($"Exception inside SchedulerDeamon: {e.Message} \n ***********************************************************************************\n", e);
                Telemetry.TrackException(moreInfo);

                WorkBench.ConsoleWriteLine(e.ToString());
            }
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}