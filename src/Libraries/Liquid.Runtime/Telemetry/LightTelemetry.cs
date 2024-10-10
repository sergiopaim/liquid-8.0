using Liquid.Base;
using Liquid.Interfaces;
using System;
using System.Collections.Generic;

namespace Liquid.Runtime.Telemetry
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// LightTelemetry implements only part of the management of aggregate metrics.
    /// According to the specification, all aggregate telemetry monitoring events will be managed by this class.
    /// Other features will be implemented by the lower-level class in the AppInsights case.
    /// </summary>
    public abstract class LightTelemetry : ILightTelemetry
    {
        private readonly Dictionary<string, LightMetric> _aggregators = [];
        /// <inheritdoc/>        
        public string OperationId { get; set; }
        /// <inheritdoc/>
        public abstract void TrackTrace(params object[] trace);
        /// <inheritdoc/>
        public abstract void TrackEvent(string name, string context = null);
        /// <inheritdoc/>
        public abstract void TrackMetric(string metricLabel, double value);
        /// <inheritdoc/>
        public abstract void TrackException(Exception exception);

        /// <inheritdoc/>
        public void ComputeMetric(string metricLabel, double value)
        {

            _aggregators.TryGetValue(metricLabel, out LightMetric lightMetricAggregator);

            if (lightMetricAggregator is null)
            {
                throw new LightException($"There is no metric  \"{metricLabel}\" under aggregation.");
            }

            lightMetricAggregator.TrackValue(value);
        }

        /// <inheritdoc/>
        public abstract void TrackAggregateMetric(object metricTelemetry);

        /// <inheritdoc/>
        public void BeginMetricComputation(string metricLabel)
        {
            _aggregators.TryGetValue(metricLabel, out LightMetric lightMetricAggregator);

            if (lightMetricAggregator is not null)
            {
                throw new LightException($"The metric \"{metricLabel}\" is already been aggregated.");
            }

            _aggregators.Add(metricLabel, new LightMetric(metricLabel));
        }

        /// <inheritdoc/>
        public void EndMetricComputation(string metricLabel)
        {

            _aggregators.TryGetValue(metricLabel, out LightMetric lightMetricAggregator);

            if (lightMetricAggregator is null)
            {
                throw new LightException($"There is no metric  \"{metricLabel}\" under aggregation.");
            }
            else
            {
                _aggregators.Remove(metricLabel);

                lightMetricAggregator.SendAggregationMetrics();
            }
        }

        /// <inheritdoc/> 
        public abstract void Initialize();

        /// <inheritdoc/>        
        public abstract void EnqueueContext(string parentID, object value = null, string operationID = "");

        /// <inheritdoc/>
        public abstract void DequeueContext();

        /// <inheritdoc/>
        public abstract LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value);
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}