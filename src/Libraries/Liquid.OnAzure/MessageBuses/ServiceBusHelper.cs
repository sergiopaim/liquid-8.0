using Azure.Messaging.ServiceBus;
using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Liquid.Runtime.Telemetry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Liquid.OnAzure
{
    internal class ServiceBusHelper 
    {
        internal static async Task ErrorHandlerAsync(ProcessErrorEventArgs args)
        {
            if (args.Exception is not ObjectDisposedException &&
                args.Exception is not OperationCanceledException &&
                (args.Exception is not ServiceBusException || (args.Exception as ServiceBusException).Reason != ServiceBusFailureReason.MessagingEntityDisabled))
                WorkBench.BaseTelemetry.TrackException(args?.Exception);

            await Task.CompletedTask;
        }

        internal static async Task HandleExceptionAsync(Exception ex, ProcessMessageEventArgs args, ServiceBusProcessor Processor)
        {
            Exception moreInfo = new LightException($"Exception reading message from {args.EntityPath}. See inner exception for details. Message={ex.Message}", ex);
            //Use the class instead of interface because tracking exceptions directly is not supposed to be done outside AMAW (i.e. by the business code)
            WorkBench.BaseTelemetry.TrackException(moreInfo);

            //If there is a business error or an invalid input, set DeadLetter on register
            if (Processor.ReceiveMode == ServiceBusReceiveMode.PeekLock)
                if (ex.InnerException is not null)
                {
                    var errorDescription = $"EXCEPTION: {ex.InnerException}";

                    if (ex is OptimisticConcurrencyLightException)
                        await SendToDeadLetter(args, errorDescription, $"Optimistic conflict error occurred", [new() { Code = "OPTIMISTIC_CONCURRENCY_CONFLICT", Message = ex.Message, Type = CriticType.Error }]);
                    else if (ex.InnerException is InvalidInputLightException)
                        await SendToDeadLetter(args, errorDescription, $"Invalid (message) input errors occurred", (ex.InnerException as InvalidInputLightException).InputErrors);
                    else if (ex.InnerException is BusinessValidationLightException)
                        await SendToDeadLetter(args, errorDescription, $"Critical business errors occurred", (ex.InnerException as BusinessValidationLightException).InputErrors);
                    else
                        await SendToDeadLetter(args, errorDescription, "Inner general unhandled exception");
                }
                else
                    await SendToDeadLetter(args, $"EXCEPTION: {ex}", "General unhandled exception");
        }

        private static async Task SendToDeadLetter(ProcessMessageEventArgs args, string errorDescription, string reason, List<Critic> inputErrors = null)
        {
            //Truncates to the maximun allowed by the telemetry
            if (errorDescription.Length > 4096)
                errorDescription = errorDescription.Truncate(4092) + "(..)";

            if (inputErrors?.Count > 0)
                reason += ":\n" + (new { critics = inputErrors }).ToJsonString();

            WorkBench.BaseTelemetry.TrackException(new DeadLetterLightException(reason, errorDescription));

            await args.DeadLetterMessageAsync(args.Message, deadLetterReason: reason, errorDescription);
        }
    }
}