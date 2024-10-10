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
using System.Threading.Tasks;

namespace Liquid.OnAzure
{
    /// <summary>
    /// Implementation of the communication component between queues and topics of the Azure, this class is specific to azure
    /// </summary>
    public class ServiceBusWorker : LightWorker, IWorkBenchHealthCheck
    {
        private static readonly Dictionary<string, (ServiceBusProcessor Processor, MethodInfo Method)> workers = [];

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
                        MaxDelay = TimeSpan.FromSeconds(30)
                    }
                };
                return clientOptions;
            }
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            WorkBench.ConsoleWriteLine("Starting Service Bus Worker");

            base.Initialize();

            MapQueueWorkersAsync().Wait();
            MapSubscriptionWorkersAsync().Wait();

            //Starts all workers
            foreach (var worker in workers)
                worker.Value.Processor.StartProcessingAsync().Wait();

            WorkBench.ConsoleWriteLine("Service Bus Worker started");
        }

        /// <summary>
        /// Maps queue worker methods
        /// </summary>
        private async Task MapQueueWorkersAsync()
        {
            try
            {
                foreach (var queue in Queues)
                {
                    MethodInfo method = GetMethod(queue);
                    string queueName = queue.Value.QueueName;

                    ServiceBusReceiveMode receiveMode = queue.Value.DeleteAfterRead
                                                            ? ServiceBusReceiveMode.ReceiveAndDelete
                                                            : ServiceBusReceiveMode.PeekLock;

                    int maxConcurrentCalls = queue.Value.MaxConcurrentCalls;

                    ServiceBusClient client = new(GetConnection(queue), ClientOptions);
                    ServiceBusAdministrationClient adminClient = new(GetConnection(queue));

                    try
                    {
                        await adminClient.CreateQueueAsync(queueName);
                    }
                    catch (ServiceBusException e)
                        when (e.Reason is ServiceBusFailureReason.MessagingEntityAlreadyExists)
                    { }

                    var processor = client.CreateProcessor(queueName, options: new() { ReceiveMode = receiveMode, MaxConcurrentCalls = maxConcurrentCalls, AutoCompleteMessages = false });
                    processor.ProcessMessageAsync += MessageHandlerAsync;
                    processor.ProcessErrorAsync += ServiceBusHelper.ErrorHandlerAsync;

                    workers.Add(processor.Identifier, (processor, method));
                }
            }
            catch (Exception exception)
            {
                Exception moreInfo = new LightException($"Error setting up queue consumption from service bus. See inner exception for details. Message={exception.Message}", exception);
                //Use the class instead of interface because tracking exceptions directly is not supposed to be done outside AMAW (i.e. by the business code)
                WorkBench.BaseTelemetry.TrackException(moreInfo);
            }
        }

        /// <summary>
        /// Maps topic subscription worker methods
        /// </summary>
        private async Task MapSubscriptionWorkersAsync()
        {
            const string FILTER_RULE_NAME = "SqlFilter";

            try
            {
                foreach (var topic in Topics)
                {
                    MethodInfo method = GetMethod(topic);

                    var args = method.CustomAttributes.Where(a => a.AttributeType == typeof(TopicAttribute)).FirstOrDefault().ConstructorArguments.LastOrDefault();

                    string sqlFilterArgs = args.ArgumentType == typeof(string) ? args.Value.ToString() : "1 = 1";
                    if (string.IsNullOrWhiteSpace(sqlFilterArgs))
                        sqlFilterArgs = "1 = 1";

                    var filterRuleDescription = new CreateRuleOptions()
                    {
                        Name = FILTER_RULE_NAME,
                        Filter = new SqlRuleFilter(sqlFilterArgs)
                    };

                    string topicName = topic.Value.TopicName;
                    string subscriptionName = topic.Value.Subscription;

                    ServiceBusReceiveMode receiveMode = topic.Value.DeleteAfterRead
                                                            ? ServiceBusReceiveMode.ReceiveAndDelete
                                                            : ServiceBusReceiveMode.PeekLock;

                    int maxConcurrentCalls = topic.Value.MaxConcurrentCalls;

                    ServiceBusAdministrationClient adminClient = new(GetConnection(topic));
                    ServiceBusClient client = new(GetConnection(topic), ClientOptions);

                    try
                    {
                        await adminClient.CreateTopicAsync(topicName);
                    }
                    catch (ServiceBusException e)
                        when (e.Reason is ServiceBusFailureReason.MessagingEntityAlreadyExists)
                    { }

                    try
                    {
                        await adminClient.CreateSubscriptionAsync(new(topicName, subscriptionName), filterRuleDescription);
                    }
                    catch (ServiceBusException e)
                        when (e.Reason is ServiceBusFailureReason.MessagingEntityAlreadyExists)
                    {
                        RuleProperties rule = null;
                        try
                        {
                            rule = await adminClient.GetRuleAsync(topicName, subscriptionName, FILTER_RULE_NAME);
                        }
                        catch { }

                        if (rule is null)
                            await adminClient.CreateRuleAsync(topicName, subscriptionName, filterRuleDescription);
                        else if (!rule.Filter.Equals(filterRuleDescription.Filter))
                        {
                            rule.Filter = filterRuleDescription.Filter;
                            await adminClient.UpdateRuleAsync(topicName, subscriptionName, rule);
                        }
                    }

                    var processor = client.CreateProcessor(topicName, subscriptionName, options: new() { ReceiveMode = receiveMode, MaxConcurrentCalls = maxConcurrentCalls, AutoCompleteMessages = false });

                    processor.ProcessMessageAsync += MessageHandlerAsync;
                    processor.ProcessErrorAsync += ServiceBusHelper.ErrorHandlerAsync;

                    workers.Add(processor.Identifier, (processor, method));
                }
            }
            catch (Exception exception)
            {
                Exception moreInfo = new LightException($"Error setting up subscription consumption from service bus. See inner exception for details. Message={exception.Message}", exception);
                //Use the class instead of interface because tracking exceptions directly is not supposed to be done outside AMAW (i.e. by the business code)
                WorkBench.BaseTelemetry.TrackException(moreInfo);
                WorkBench.ConsoleWriteLine(exception.ToString());
            }
        }

        private async Task MessageHandlerAsync(ProcessMessageEventArgs args)
        {
            var (Processor, Method) = workers[args.Identifier];

            try
            {
                InvokeWorker(Method, args.Message.Body);
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                await ServiceBusHelper.HandleExceptionAsync(ex, args, Processor);
            }
        }

        private static string GetConnection<T>(KeyValuePair<MethodInfo, T> item)
        {
            MethodInfo method = item.Key;
            string connectionKey = GetKeyConnection(method);
            ServiceBusConfiguration config = null;
            if (string.IsNullOrWhiteSpace(connectionKey)) // Load specific settings if provided
                config = LightConfigurator.LoadConfig<ServiceBusConfiguration>($"{nameof(ServiceBus)}");
            else
                config = LightConfigurator.LoadConfig<ServiceBusConfiguration>($"{nameof(ServiceBus)}_{connectionKey}");

            return config.ConnectionString;
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