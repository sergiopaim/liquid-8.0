using Liquid.Base;
using Liquid.Domain.API;
using System;

namespace Liquid.Domain.Test
{
    /// <summary>
    /// Helper class to test Scheduler jobs
    /// </summary>
    /// <remarks>
    /// Instanciates a Scheduler tester 
    /// </remarks>
    /// <param name="bus">the messsage bus tester</param>
    public class SchedulerTester(MessageBusTester bus)
    {

        /// <summary>
        /// Dispactches a job
        /// </summary>
        /// <returns>A domain response</returns>
        public HttpResponseMessageWrapper<DomainResponse> Dispactch(string job, DateTime activation, int partition = 1)
        {
            JobDispatchMSG msg = new()
            {
                Activation = activation,
                Partition = partition,
                Job = job,
                CommandType = JobDispatchCMD.Trigger.Code
            };

            return bus.SendToTopic($"messageBus/send/topic/{SchedulerMessageBus<MessageBrokerWrapper>.JOBS_ENDPOINT}", msg);
        }
    }
}