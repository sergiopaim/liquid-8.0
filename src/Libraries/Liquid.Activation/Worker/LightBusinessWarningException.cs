using System;

namespace Liquid.Activation
{
    [Serializable]
    internal class LightBusinessWarningException : Exception
    {
        public LightBusinessWarningException() { }
        public LightBusinessWarningException(string message) : base(message) { }
        public LightBusinessWarningException(string message, Exception innerException) : base(message, innerException) { }
    }
}