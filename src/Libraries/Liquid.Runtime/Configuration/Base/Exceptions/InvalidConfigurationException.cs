using System;
using System.Collections.Generic;

namespace Liquid.Runtime
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    [Serializable]
    public class InvalidConfigurationException : Exception
    {
        public Dictionary<string, object[]> InputErrors { get; } = [];

        public InvalidConfigurationException(string message) : base(message) { }

        public InvalidConfigurationException(Dictionary<string, object[]> inputErrors) : base(string.Concat(inputErrors))
        {
            InputErrors = inputErrors;
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}