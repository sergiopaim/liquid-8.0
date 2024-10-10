using Liquid.Base;
using System;

namespace Liquid.Domain
{
    /// <summary>
    /// Exception for optimistic concurrency conflict detection
    /// </summary>
    /// <remarks>
    /// Building a LightException with summary data
    /// </remarks>
    /// <param name="modelName">The name of the model entity</param>
    [Serializable]
    public class OptimisticConcurrencyLightException(string modelName) : LightException($"An optimistic concurrence conflict happend in repository for a 'LightOptimisticModel<{modelName}>' record.") { }
}