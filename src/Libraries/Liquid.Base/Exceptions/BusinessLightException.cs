using System;
using System.Runtime.Serialization;

namespace Liquid.Base
{
    /// <summary>
    /// Class responsible for building the Business Exceptions
    /// </summary>
    /// <remarks>
    /// Throws a business exception with contextual information
    /// </remarks>
    /// <param name="businessCode">The code to identify the point of business failure</param>
    [Serializable]
    public class BusinessLightException(string businessCode) : LightException(businessCode.Replace(" ", "_").ToUpper()), ISerializable { }
}