using Liquid.Base;
using System;

namespace Liquid.Domain
{
    /// <summary>
    /// Exception for duplicated insertion conflict detection
    /// </summary>
    /// <remarks>
    /// Building a LightException with summary data
    /// </remarks>
    /// <param name="modelName">The name of the model entity</param>
    [Serializable]
    public class DuplicatedInsertionLightException(string modelName) : LightException($"An insertion conflict happend in repository for a '{modelName}' record.") { }
}