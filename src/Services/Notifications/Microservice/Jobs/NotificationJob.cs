using Liquid;
using Liquid.Activation;
using Microservice.Services;
using System;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0060 // Remove unused parameter
namespace Microservice.Jobs
{
    [Scheduler("TRANSACTIONAL", "notifications")]
    public class NotificationJob : LightJobScheduler
    {
        [Job(nameof(LightJobFrequency.EveryTenMinutes))]
        public async void ReinforceByEmail(DateTime activation, int partition)
        {
            await Factory<NotificationService>().ReinforceByEmailAsync(activation.AddMinutes(-30), activation.AddMinutes(-20));

            Terminate();
        }

        [Job(nameof(LightJobFrequency.EveryTenMinutes))]
        public async void RetrieveEmailBounces(DateTime activation, int partition)
        {
            //Only cares about bounces from production
            if (WorkBench.IsProductionEnvironment)
                await Factory<MSGraphService>().RetrieveEmailBouncesAsync(activation.AddMinutes(-10), activation);

            Terminate();
        }
    }
}
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member