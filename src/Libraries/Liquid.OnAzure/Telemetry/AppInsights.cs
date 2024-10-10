using Liquid.Base;
using Liquid.Interfaces;
using Liquid.Runtime;
using Liquid.Runtime.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Diagnostics;

namespace Liquid.OnAzure
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// The AppInsights class is the lowest level of integration of WorkBench with Azure AppInsights.
    /// It directly provides a client to send the messages to the cloud.
    /// So it is possible to trace all logs, events, traces, exceptions in an aggregated and easy-to-use form.
    /// </summary>
    public class AppInsights : LightTelemetry, ILightTelemetry
    {
        //TelemetryClient is responsible for sending all telemetry requests to the azure.
        //It is still possible to make settings regarding the hierarchy of messages.
        //This setting is changeable as desired by the developer.
        private static TelemetryClient TelemetryClient;

        public new string OperationId
        {
            get => TelemetryClient?.Context?.Operation?.Id;
            set
            {
                if (TelemetryClient?.Context?.Operation is not null)
                    TelemetryClient.Context.Operation.Id = value;

                Activity.Current?.SetBaggage("operationId", value);
            }
        }

        public AppInsights() { }

        /// <inheritdoc/>
        public override void TrackEvent(string name, string context = null)
        {
            var eventTelemetry = new EventTelemetry() { Name = name };
            eventTelemetry.Context.Operation.Id = OperationId;

            if (context is not null)
                eventTelemetry.Properties.Add("Context", context);

            TelemetryClient.TrackEvent(eventTelemetry);
        }

        /// <inheritdoc/>
        public override void TrackMetric(string metricLabel, double value)
        {
            var metric = new MetricTelemetry() { Name = metricLabel, Sum = value };
            metric.Context.Operation.Id = OperationId;

            TelemetryClient.TrackMetric(metric);
        }

        /// <inheritdoc/>
        public override void TrackTrace(params object[] trace)
        {
            var traceTelemetry = new TraceTelemetry() { Message = (string)trace?[0] };
            traceTelemetry.Context.Operation.Id = OperationId;

            TelemetryClient.TrackTrace(traceTelemetry);
        }

        /// <inheritdoc/>
        public override void TrackAggregateMetric(object metricTelemetry)
        {
            TelemetryClient.TrackMetric((MetricTelemetry)metricTelemetry);
        }

        /// <inheritdoc/>
        public override void TrackException(Exception exception)
        {
            var exceptionTelemetry = new ExceptionTelemetry() { Exception = exception };
            exceptionTelemetry.Context.Operation.Id = OperationId;

            TelemetryClient.TrackException(exceptionTelemetry);
        }

        /// <inheritdoc/>       
        public override void Initialize()
        {
            AppInsightsConfiguration appInsightsConfiguration = LightConfigurator.LoadConfig<AppInsightsConfiguration>("ApplicationInsights");

            TelemetryConfiguration aiConfig = new()
            {
                ConnectionString = appInsightsConfiguration.ConnectionString
            };

            aiConfig.TelemetryProcessorChainBuilder
                    .Use((next) => new LightTelemetryProcessor(next))
                    .Build();

            // automatically correlate all telemetry data with request
            aiConfig.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());

            TelemetryClient ??= new(aiConfig);
            TelemetryClient.Context.Cloud.RoleName = "API";
        }

        private delegate void WrapperTelemetry(string ParentID, object Value, string OperationID, TelemetryClient telemetryClient);

        private readonly WrapperTelemetry wrapper = (parent, value, operation, telemtry) =>
        {
            telemtry.Context.Operation.ParentId = parent;
            telemtry.Context.Operation.Id = !string.IsNullOrWhiteSpace(operation)
                                                ? operation
                                                : Guid.NewGuid().ToString();

            telemtry.Context.Operation.Name = $"{parent}/{operation}";
        };

        /// <inheritdoc/>
        public override void EnqueueContext(string parentId, object value = null, string operationId = "")
        {
            wrapper.Invoke(parentId, value, operationId ?? OperationId, TelemetryClient);
        }

        /// <inheritdoc/>
        public override void DequeueContext()
        {
            TelemetryClient.Context.Operation.ParentId = null;
            TelemetryClient.Context.Operation.Id = null;
            TelemetryClient.Context.Operation.Name = null;
        }

        /// <inheritdoc/>
        public override LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value)
        {
            try
            {
                TelemetryClient.TrackEvent("Method invoked");
                return LightHealth.HealthCheckStatus.Healthy;
            }
            catch
            {
                return LightHealth.HealthCheckStatus.Unhealthy;
            }
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}