using Liquid.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Liquid.Domain
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// Class indicates the errors on the model layer
    /// </summary>
    [Serializable()]
    public class InvalidModelLightException : LightException
    {
        public List<Critic> InputErrors { get; } = [];

        public override string Message { get; }

        public InvalidModelLightException(string modelName, Dictionary<string, object[]> inputErrors) : base()
        {
            Message = $"Invalid model '{modelName}'. Check the errors";
            InputErrors.Clear();

            if (inputErrors is null)
                return;

            foreach (var error in inputErrors)
            {
                Critic critic = new();
                critic.AddError(error.Key, CriticHandler.LocalizeMessage(error.Key, error.Value));
                InputErrors.Add(critic);
            }
        }

        public override string ToString()
        { 
            return $"({nameof(InvalidModelLightException)}) {Message}:\n                                           * " + 
                   string.Join("\n                                           * ", InputErrors.Select(e => e.Code)) +
                   "\n\n";
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}