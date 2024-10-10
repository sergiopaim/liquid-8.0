using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Reflection;

namespace Liquid.Runtime
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SwaggerIgnoreFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema?.Properties is null)
                return;

            var excludedProperties = context?.Type.GetProperties().Where(t => t.GetCustomAttribute<SwaggerIgnoreAttribute>() is not null);

            foreach (var excludedProperty in excludedProperties)
            {
                var propertyToRemove = schema.Properties.Keys.SingleOrDefault(x => x.Equals(excludedProperty.Name, System.StringComparison.CurrentCultureIgnoreCase));
                if (propertyToRemove is not null)
                    schema.Properties.Remove(propertyToRemove);
            }
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}