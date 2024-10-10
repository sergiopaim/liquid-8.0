using System;

namespace Liquid.Activation
{
    /// <summary>
    /// Attribute used for connect a Service Bus (Queue / Topic).
    /// </summary>
    /// <remarks>
    /// Constructor used to inform a Configuration Tag Name.
    /// </remarks>
    /// <param name="configTagName">Configuration Tag Name</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class MessageBusAttribute(string configTagName) : Attribute
    {
        /// <summary>
        /// Get a Configuration Tag Name.
        /// </summary>
        public virtual string ConfigTagName
        {
            get { return configTagName; }
        }
    }
}