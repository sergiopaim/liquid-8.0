using Liquid.Domain;
using System;

namespace Liquid.Activation
{
    /// <summary>
    /// Attribute used for connect a Queue.
    /// </summary>
    /// <remarks>
    /// Constructor used to inform a Queue name.
    /// </remarks>
    /// <param name="queueName">Queue Name</param>
    /// <param name="maxConcurrentCalls">Quantity to take in unit process, by default 10</param>
    /// <param name="deleteAfterRead">Delete after read the message? by default true</param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QueueAttribute(string queueName, int maxConcurrentCalls = 10, bool deleteAfterRead = true) : Attribute
    {
        /// <summary>
        /// Get a Queue Name.
        /// </summary>
        public virtual string QueueName { get; } = MessageBrokerWrapper.BuildNonProductionEnvironmentEndpointName(queueName);

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