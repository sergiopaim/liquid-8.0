using System;
using System.Threading;
namespace Liquid.Runtime.Telemetry
{
    /// <summary>
    /// Aggregates metric values for a single time period.
    /// Responsible for doing all logic aggregation, summarization, weighted average given an event defined by the developer.
    /// For start this aggregation, its necessary call BeginComputeMetric and then call EndMetricComputation.
    /// </summary>
    ///Constructor necessary for take the timestamp that the matric will be tracked
    internal class LightMetricAggregator(DateTimeOffset startTimestamp)
    {
        private readonly SpinLock _trackLock;

        public DateTimeOffset StartTimestamp { get; } = startTimestamp;
        public int Count { get; private set; }
        public double Sum { get; private set; }
        public double SumOfSquares { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Average { get { return (Count == 0) ? 0 : (Sum / Count); } }
        public double Variance
        {
            get
            {
                return (Count == 0) ? 0 : (SumOfSquares / Count) - (Average * Average);
            }
        }
        public double StandardDeviation { get { return Math.Sqrt(Variance); } }

        ///Trace values ​​to be aggregated defining minimum, maximum average, time and sum of squares.
        public void TrackValue(double value)
        {
            bool lockAcquired = false;

            try
            {
                _trackLock.Enter(ref lockAcquired);

                if ((Count == 0) || (value < Min)) { Min = value; }
                if ((Count == 0) || (value > Max)) { Max = value; }
                Count++;
                Sum += value;
                SumOfSquares += value * value;
            }
            finally
            {
                if (lockAcquired)
                {
                    _trackLock.Exit();
                }
            }
        }
    }
}