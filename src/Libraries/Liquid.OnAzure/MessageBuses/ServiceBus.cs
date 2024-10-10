using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Liquid.Runtime;
using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Liquid.OnAzure
{
    /// <summary>
    /// Implementation of the communication component between queues of the Azure, this class is specific to azure
    /// </summary>
    public class ServiceBus : MessageBrokerWrapper
    {
        private const int DAYS_TO_LIVE = 365;

        private ServiceBusAdministrationClient adminClient;
        private ServiceBusClient client;
        private readonly ConcurrentDictionary<string, ServiceBusSender> senders = new();

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

        /// <summary>
        /// Inicialize the class with set Config Name and Queue Name, must called the parent method
        /// </summary>
        /// <param name="tagConfigName"> Config Name </param>
        /// <param name="endpointName">Queue Name</param> 
        public override void Config(string tagConfigName, string endpointName)
        {
            base.Config(tagConfigName, endpointName);
            SetConnection(tagConfigName);
        }

        /// <summary>
        /// Get connection settings
        /// </summary>
        /// <param name="tagConfigName"></param>
        private void SetConnection(string tagConfigName)
        {
            MessageBrokerConfiguration config = null;
            if (string.IsNullOrWhiteSpace(tagConfigName)) // Load specific settings if provided
                config = LightConfigurator.LoadConfig<MessageBrokerConfiguration>(nameof(ServiceBus));
            else
                config = LightConfigurator.LoadConfig<MessageBrokerConfiguration>($"{nameof(ServiceBus)}_{tagConfigName}");

            adminClient = new(config.ConnectionString);
            client = new(config.ConnectionString, ClientOptions);
        }

        /// <summary>
        /// Sends a message to a queue
        /// </summary>
        /// <typeparam name="T">Type of message to send</typeparam>
        /// <param name="message">Object of message to send</param>
        /// <param name="queueName">Name of the queue</param>
        /// <param name="subjectLabel">Label of the message</param>
        /// <param name="minutesToLive">Message's time-to-live in minutes (default 365 days)</param>
        /// <param name="minutesToDelay">Message's delay to be processed in minutes (default 0)</param>
        /// <returns>The task of Process topic</returns> 
        public override async Task SendToQueueAsync<T>(T message, string queueName = null, string subjectLabel = null, int? minutesToLive = null, int? minutesToDelay = null)
        {
            var endpoint = queueName ?? EndpointName;
            ServiceBusSender sender;

            if (senders.ContainsKey(endpoint))
                senders.TryGetValue(endpoint, out sender);
            else
            {
                sender = client.CreateSender(endpoint);
                senders.TryAdd(endpoint, sender);
            }

            var messageData = new ServiceBusMessage(message.ToJsonBytes())
            {
                ContentType = "application/json;charset=utf-8",
                Subject = subjectLabel ?? typeof(T).ToString(),
                MessageId = Guid.NewGuid().ToString(),
                TimeToLive = minutesToLive is null
                                ? TimeSpan.FromDays(DAYS_TO_LIVE)
                                : TimeSpan.FromMinutes(minutesToLive.Value)
            };

            if (minutesToDelay.HasValue && minutesToDelay.Value > 0)
                messageData.ScheduledEnqueueTime = DateTime.UtcNow.AddMinutes(minutesToDelay.Value);

            try
            {
                await sender.SendMessageAsync(messageData);
            }
            catch (ServiceBusException ex)
                when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                try
                {
                    await adminClient.CreateQueueAsync(endpoint);
                    await sender.SendMessageAsync(messageData);
                }
                catch (Exception e)
                {
                    TrackMissedMessage(endpoint, message, e);
                }
            }
            catch (Exception e)
            {
                TrackMissedMessage(endpoint, message, e);
            }
        }

        /// <summary>
        /// Sends a message to a topic
        /// </summary>
        /// <typeparam name="T">Type of message to send</typeparam>
        /// <param name="message">Object of message to send</param>
        /// <param name="topicName">Name of the topic</param>
        /// <param name="subjectLabel">Label of the message</param>
        /// <param name="minutesToLive">Message's time-to-live in minutes (default 365 days)</param>
        /// <param name="minutesToDelay">Message's delay to be processed in minutes (default 0)</param>
        /// <returns>The task of Process topic</returns> 
        public override async Task SendToTopicAsync<T>(T message, string topicName = null, string subjectLabel = null, int? minutesToLive = null, int? minutesToDelay = null)
        {
            var endpoint = topicName ?? EndpointName;

            ServiceBusSender sender;

            if (senders.ContainsKey(endpoint))
                senders.TryGetValue(endpoint, out sender);
            else
            {
                sender = client.CreateSender(endpoint);
                senders.TryAdd(endpoint, sender);
            }

            var messageData = new ServiceBusMessage(message.ToJsonBytes())
            {
                ContentType = "application/json;charset=utf-8",
                Subject = subjectLabel ?? typeof(T).ToString(),
                MessageId = Guid.NewGuid().ToString(),
                TimeToLive = minutesToLive is null
                                ? TimeSpan.FromDays(DAYS_TO_LIVE)
                                : TimeSpan.FromMinutes(minutesToLive.Value)
            };

            if (minutesToDelay.HasValue && minutesToDelay.Value > 0)
                messageData.ScheduledEnqueueTime = DateTime.UtcNow.AddMinutes(minutesToDelay.Value);

            foreach (var kvp in message.GetUserProperties())
                messageData.ApplicationProperties.Add(kvp.Key, kvp.Value);

            try
            {
                await sender.SendMessageAsync(messageData);
            }
            catch (ServiceBusException ex) 
                when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                try
                {
                    await adminClient.CreateTopicAsync(endpoint);
                    await sender.SendMessageAsync(messageData);
                }
                catch (Exception e)
                {
                    TrackMissedMessage(endpoint, message, e);
                }
            }
            catch (Exception e)
            {
                TrackMissedMessage(endpoint, message, e);
            }
        }

        private static void TrackMissedMessage<T>(string endpoint, T message, Exception sourceException) where T : ILightMessage
        {
            WorkBench.BaseTelemetry.TrackException(new MessageMissedLightException(endpoint, sourceException));

            //removes sensitive information
            JsonNode msgAsJson = message.ToJsonNode();

            msgAsJson["tokenJwt"] = null;

            WorkBench.BaseTelemetry.TrackTrace(msgAsJson.ToJsonString(true));
        }
    }
}