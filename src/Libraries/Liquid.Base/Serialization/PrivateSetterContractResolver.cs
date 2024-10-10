using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Liquid.Base
{
    /// <summary>
    /// Setter contractor for updating private properties
    /// </summary>
    public class PrivateSetterContractResolver : DefaultJsonTypeInfoResolver
    {
        /// <summary>
        /// Constructs a new setter contractor
        /// </summary>
        public PrivateSetterContractResolver()
        {
            Modifiers.Add(IncludePrivateSetters);
        }

        private void IncludePrivateSetters(JsonTypeInfo jsonTypeInfo)
        {
            if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
                foreach (var property in jsonTypeInfo.Properties)
                    if (property.Get is not null && property.Set is null)
                    {
                        var propertyInfo = jsonTypeInfo.Type.GetProperty(property.Name.FirstToUpper(), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                        //Defensivelly retries without PascalCase
                        propertyInfo ??= jsonTypeInfo.Type.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (propertyInfo is not null)
                        {
                            var privateSetter = propertyInfo.GetSetMethod(true);
                            if (privateSetter is not null)
                                property.Set = (obj, value) => privateSetter.Invoke(obj, [value]);
                        }
                    }
        }
    }
}
