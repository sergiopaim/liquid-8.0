using Liquid.Interfaces;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;

namespace Liquid.Runtime.Telemetry
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class LightTelemetryProcessor(ITelemetryProcessor next) : ITelemetryProcessor
    {
        private readonly AdaptiveSamplingTelemetryProcessor sampler = new(next);
        public ITelemetryProcessor next = next;

        public void Process(ITelemetry item)
        {
            if (ShouldIgnore(item))
                return;
            else if (ShouldAlwaysSend(item))
                next.Process(item);
            else
                sampler.Process(item);
        }

        private static bool ShouldIgnore(ITelemetry item)
        {
            // Filter out success dependency calls
            if (item is DependencyTelemetry dependency)
                return dependency.Success == true;

            // Filter out GET /health and OPTIONS requests
            if (item is RequestTelemetry request && (request.Name == "GET /health" || request.Name.StartsWith("OPTIONS")))
                return true;

            return false;
        }

        private static bool ShouldAlwaysSend(ITelemetry telemetry)
        {
            if (telemetry is ExceptionTelemetry ||
                telemetry is EventTelemetry ||
                telemetry is TraceTelemetry ||
                telemetry is DependencyTelemetry)
                return true;

            if (telemetry is RequestTelemetry request &&
                request.ResponseCode != ((int)StatusCode.OK).ToString() &&
                request.ResponseCode != ((int)StatusCode.PartialContent).ToString() &&
                request.ResponseCode != ((int)StatusCode.NoContent).ToString())
                return true;

            return false;
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}