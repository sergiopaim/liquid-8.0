using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Liquid.Activation;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Liquid.Runtime;
using Liquid.Runtime.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Liquid.OnAzure
{
    /// <summary>
    /// Scheduler job base class 
    /// </summary>
    public class ServiceBusScheduler : LightJobScheduler, IWorkBenchHealthCheck
    {
        private static readonly int OPERATION_TIME_OUT_IN_MINUTES = 15;
        private static ServiceBusProcessor Processor { get; set; }
        private static SchedulerMessageBus<ServiceBus> SchedulerMessageBus { get; set; }

        private static ServiceBusClientOptions clientOptions;
        /// <summary>
        /// The options used
        /// </summary>
        public static ServiceBusClientOptions ClientOptions
        {
            get
            {
                clientOptions ??= new()
                {
                    RetryOptions = new()
                    {
                        Mode = ServiceBusRetryMode.Exponential,
                        MaxRetries = 10,
                        Delay = TimeSpan.FromSeconds(3),
                        MaxDelay = TimeSpan.FromSeconds(30),
                        TryTimeout = TimeSpan.FromMinutes(OPERATION_TIME_OUT_IN_MINUTES)
                    }
                };
                return clientOptions;
            }
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            base.Initialize();

            MapJobsAsync().Wait();
            Processor.StartProcessingAsync().Wait();

            Initialized();
        }

        private static async Task MapJobsAsync()
        {
            const string FILTER_RULE_NAME = "JobFilter";

            if (Jobs.Count <= 0) return;

            try
            {
                var refJob = Jobs.First();
                var config = GetConfigFile(refJob);
                var connectionKey = GetConnectionKey(refJob.Key);

                var dispatchEndpoint = SchedulerMessageBus<ServiceBus>.DispatchEndpoint;
                var subscriptionName = GetSubscriptionName(refJob.Key);

                SchedulerMessageBus = new(connectionKey);

                await RegisterJobsAsync(subscriptionName);

                ServiceBusAdministrationClient adminClient = new(config.ConnectionString);
                ServiceBusClient client = new(config.ConnectionString, ClientOptions);

                var filterRuleDescription = new CreateRuleOptions()
                {
                    Name = FILTER_RULE_NAME,
                    Filter = new SqlRuleFilter($"sys.Label = '{subscriptionName}'")
                };

                try
                {
                    await adminClient.CreateTopicAsync(dispatchEndpoint);
                }
                catch (ServiceBusException e)
                    when (e.Reason is ServiceBusFailureReason.MessagingEntityAlreadyExists)
                { }

                try
                {
                    await adminClient.CreateSubscriptionAsync(new(dispatchEndpoint, subscriptionName), filterRuleDescription);
                }
                catch (ServiceBusException e)
                    when (e.Reason is ServiceBusFailureReason.MessagingEntityAlreadyExists)
                { }

                var maxConcurrentCalls = GetMaxConcurrentCalls(refJob.Key);

                Processor = client.CreateProcessor(dispatchEndpoint, subscriptionName, options: new() { ReceiveMode = ServiceBusReceiveMode.PeekLock, MaxConcurrentCalls = maxConcurrentCalls, AutoCompleteMessages = false, PrefetchCount = 1 });

                Processor.ProcessMessageAsync += MessageHandlerAsync;
                Processor.ProcessErrorAsync += ServiceBusHelper.ErrorHandlerAsync;
            }
            catch (Exception exception)
            {
                Exception moreInfo = new LightException($"Error setting up subscription consumption from service bus. See inner exception for details. Message={exception.Message}", exception);
                //Use the class instead of interface because tracking exceptions directly is not supposed to be done outside AMAW (i.e. by the business code)
                WorkBench.BaseTelemetry.TrackException(moreInfo);
                WorkBench.ConsoleWriteLine(exception.ToString());
                throw;
            }
        }

        private static async Task MessageHandlerAsync(ProcessMessageEventArgs args)
        {
            var message = JsonSerializer.Deserialize<JobDispatchMSG>(Encoding.UTF8.GetString(args.Message.Body),
                                                                     LightGeneralSerialization.IgnoreCase);

            if (message is null)
                return;

            try
            {
                var found = TryInvokeJob(message);

                await args.CompleteMessageAsync(args.Message);

                if (!found)
                    await FeedbackForJob(message, JobCommandCMD.NotFound);
                else
                    await FeedbackForJob(message, JobCommandCMD.Acknowledge);
            }
            catch (Exception ex)
            {
                await ServiceBusHelper.HandleExceptionAsync(ex, args, Processor);
            }
        }

        private static async Task FeedbackForJob(JobDispatchMSG processedMessage, JobCommandCMD command)
        {
            processedMessage.CommandType = null; // commands are of different types, so mapping directly throws an error

            var feedback = JobCommandMSG.FactoryFrom(processedMessage);
            feedback.CommandType = command.Code;

            await SchedulerMessageBus.SendCommand(feedback);
        }

        private static async Task RegisterJobsAsync(string subscriptionName)
        {
            foreach (var job in Jobs)
            {
                var j = job.Value;
                var jobName = job.Key.Name;

                var message = new JobCommandMSG
                {
                    CommandType = JobCommandCMD.Register.Code,
                    Microservice = subscriptionName,
                    Job = jobName,
                    Frequency = j.Frequency.Code,
                    PartitionCount = j.PartitionCount,
                    DayOfMonth = j.DayOfMonth,
                    DayOfWeek = j.DayOfWeek,
                    Hour = j.Hour,
                    Minute = j.Minute,
                    Status = LightJobStatus.Running.Code
                };

                await SchedulerMessageBus.SendCommand(message);
            }
        }

        private static MessageBrokerConfiguration GetConfigFile<T>(KeyValuePair<MethodInfo, T> item)
        {
            MethodInfo method = item.Key;
            string connectionKey = GetConnectionKey(method);

            if (string.IsNullOrWhiteSpace(connectionKey)) // Load specific settings if provided
                return LightConfigurator.LoadConfig<MessageBrokerConfiguration>($"{nameof(ServiceBus)}");

            return LightConfigurator.LoadConfig<MessageBrokerConfiguration>($"{nameof(ServiceBus)}_{connectionKey}");
        }

        /// <summary>
        /// Method to run Health Check for Service Bus
        /// </summary>
        /// <param name="serviceKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value)
        {
            return LightHealth.HealthCheckStatus.Healthy;
        }
    }
}