using System;
using System.Runtime.Serialization;

namespace Liquid.Base
{
    /// <summary>
    /// Class responsible for building the Business Exceptions
    /// </summary>
    /// <remarks>
    /// Throws a message bus exception with contextual information
    /// </remarks>
    [Serializable]
    public class MessageMissedLightException(string source, Exception innerException) : LightException(source, innerException), ISerializable { }
}