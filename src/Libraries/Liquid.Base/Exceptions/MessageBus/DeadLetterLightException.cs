using System;
using System.Runtime.Serialization;

namespace Liquid.Base
{
    /// <summary>
    /// Class responsible for building the Business Exceptions
    /// </summary>
    /// <remarks>
    /// Throws a deadletter exception with contextual information
    /// </remarks>
    [Serializable]
    public class DeadLetterLightException(string reason, string error) : LightException($"{reason}\n*********\n{error}"), ISerializable { }
}