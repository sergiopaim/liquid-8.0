using System;

namespace Liquid.Activation
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ReactiveHubAttribute(string hubEndpoint = "/hub") : Attribute
    {
        public string HubEndpoint { get; } = hubEndpoint;
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}