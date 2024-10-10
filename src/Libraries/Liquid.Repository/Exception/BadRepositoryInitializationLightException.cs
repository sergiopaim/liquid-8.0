using Liquid.Base;
using System;

namespace Liquid.Repository
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    [Serializable]
    public class BadRepositoryInitializationLightException(string lightRepositoryTypeName) : LightException($"{lightRepositoryTypeName} repository not was correctly initialized. For direct instantiation, it must be constructed as the following example: {lightRepositoryTypeName} myNewRepo = new {lightRepositoryTypeName}(\"MYNEWREPO\")") { }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}