using Liquid.Base;
using Liquid.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Liquid.Domain.Test
{
    public static class MessageBusInterceptor
    {
        // Intercept by default in dev environment
        public static bool InterceptMessages { get; set; } = Environment.GetEnvironmentVariable("INTERCEPT_MESSAGES") == "true";
        public static bool ShouldInterceptMessages =>
            (WorkBench.IsDevelopmentEnvironment || WorkBench.IsIntegrationEnvironment) && InterceptMessages;

        // thred safe
        public static ConcurrentDictionary<string, ConcurrentBag<GenericInterceptedMessage>> InterceptedMessages { get; } = new();
        public static List<GenericInterceptedMessage> InterceptedMessagesByOperationId(string operationId) =>
            InterceptedMessages.TryGetValue(operationId, out ConcurrentBag<GenericInterceptedMessage> value) 
                     ? [.. value] 
                     : [];

        public static List<GenericInterceptedMessage> InterceptedMessagesByOperationIdAndMessageType(string operationId, string messageType)
        {
            var messages = InterceptedMessagesByOperationId(operationId);

            return messages.Where(m => m.MessageType == messageType).ToList();
        }

        public static void Intercept(ILightMessage message, EndpointType endpointType, string tagConfigName, string channelName)
        {
            if (!InterceptedMessages.ContainsKey(message?.OperationId))
                InterceptedMessages.TryAdd(message?.OperationId, []);
            var interceptedMessage = new GenericInterceptedMessage
            {
                Message = message.ToJsonDocument(),
                MessageType = message.GetType().Name,
                EndpointType = endpointType,
                TagConfigName = tagConfigName,
                ChannelName = channelName
            };
            InterceptedMessages[message?.OperationId].Add(interceptedMessage);
        }

        public static void ClearMessages(string operationId = null)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                InterceptedMessages.Clear();
            else if (InterceptedMessages.TryGetValue(operationId, out ConcurrentBag<GenericInterceptedMessage> value))
                value.Clear();
        }
    }
}