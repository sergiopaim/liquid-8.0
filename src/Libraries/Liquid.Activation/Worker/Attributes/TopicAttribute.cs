using Liquid.Domain;
using System;

namespace Liquid.Activation
{
    /// <summary>
    /// Attribute used for connect a Topic and Subscription.
    /// </summary>
    /// <remarks>
    /// Constructor used to inform a Topic, Subscription name and Sql Filter.
    /// </remarks>
    /// <param name="topicName">Topic Name</param>
    /// <param name="subscriberName">Subscription Name</param>
    /// <param name="maxConcurrentCalls">Number of concurrent calls the MS could do to the bus</param>
    /// <param name="deleteAfterRead">True if the message should be deleted after pickedup</param>
    /// <param name="sqlFilter">SQL Filter</param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class TopicAttribute(string topicName, string subscriberName, int maxConcurrentCalls = 10, bool deleteAfterRead = true, string sqlFilter = "") : Attribute
    {
        /// <summary>
        /// Topic Name
        /// </summary>
        public virtual string TopicName { get; } = MessageBrokerWrapper.BuildNonProductionEnvironmentEndpointName(topicName);

        /// <summary>
        /// Subscription Name
        /// </summary>
        public virtual string Subscription { get; } = subscriberName;

        /// <summary>
        /// SQL Filter string
        /// </summary>
        public virtual string SqlFilter { get; } = sqlFilter;

        /// <summary>
        /// Take Quantity
        /// </summary>
        public virtual int MaxConcurrentCalls { get; } = maxConcurrentCalls;

        /// <summary>
        /// Delete after Read
        /// </summary>
        public virtual bool DeleteAfterRead { get; } = deleteAfterRead;
    }
}