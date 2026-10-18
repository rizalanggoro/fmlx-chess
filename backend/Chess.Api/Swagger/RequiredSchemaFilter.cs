using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

public class RequiredSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type is null || schema.Properties is null || schema.Properties.Count == 0) return;

        var nullability = new NullabilityInfoContext();
        foreach (var property in context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!IsRequired(property, nullability)) continue;

            var match = schema.Properties.Keys.FirstOrDefault(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;
            (schema.Required ??= new HashSet<string>()).Add(match);
        }
    }

    private static bool IsRequired(PropertyInfo property, NullabilityInfoContext nullability)
    {
        if (property.GetIndexParameters().Length > 0) return false;
        if (property.PropertyType.IsValueType) return Nullable.GetUnderlyingType(property.PropertyType) is null;
        return nullability.Create(property).ReadState == NullabilityState.NotNull;
    }
}
