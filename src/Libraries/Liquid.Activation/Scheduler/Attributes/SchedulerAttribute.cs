using System;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SchedulerAttribute(string schedulerName, string subscriptionName, int maxConcurrentCalls = 10) : Attribute
    {
        public string SchedulerName { get; } = schedulerName;
        public string SubscriptionName { get; } = subscriptionName;
        public int MaxConcurrentCalls { get; } = maxConcurrentCalls;
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}