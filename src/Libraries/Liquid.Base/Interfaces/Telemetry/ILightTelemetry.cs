using System;

namespace Liquid.Interfaces
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public interface ILightTelemetry : IWorkBenchHealthCheck
    {
        /// <summary>
        /// The id of the current operation
        /// </summary>
        public string OperationId { get; set; }
        /// <summary>
        /// Tracks a trace telemetry
        /// </summary>
        /// <param name="trace">Objects to be traced</param>
        void TrackTrace(params object[] trace);
        /// <summary>
        /// Tracks a custom event
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="context">The contextual information (optional)</param>
        void TrackEvent(string name, string context = null);
        /// <summary>
        /// Tracks a custom metric
        /// </summary>
        /// <param name="metricLabel">The label of the metric</param>
        /// <param name="value">The value of the metric</param>
        void TrackMetric(string metricLabel, double value);
        /// <summary>
        /// Tracks an exception
        /// </summary>
        /// <param name="exception">The exception to be tracked</param>
        void TrackException(Exception exception);

        /// <summary>
        /// Precomputes a metric before sending it
        /// </summary>
        /// <param name="metricLabel">The label of the metric</param>
        /// <param name="value">The value of the metric</param>
        void ComputeMetric(string metricLabel, double value);
        /// <summary>
        /// Begins a metric computation
        /// </summary>
        /// <param name="metricLabel">The label of the metric</param>
        void BeginMetricComputation(string metricLabel);
        /// <summary>
        /// Ends a metric computation
        /// </summary>
        /// <param name="metricLabel">The label of the metric</param>
        void EndMetricComputation(string metricLabel);
        /// <summary>
        /// Enqueue a context of metric computation as a hierarchy
        /// </summary>
        /// <param name="parentId">The id of the parent context</param>
        /// <param name="value">The value to be enqueued</param>
        /// <param name="operationId">The operation id (optional)</param>
        void EnqueueContext(string parentId, object value = null, string operationId = "");
        /// <summary>
        /// Dequeue the last context of metric computation
        /// </summary>
        void DequeueContext();
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}