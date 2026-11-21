using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Crm.Api.OpenApi.Transformers;

/// <summary>
/// Removes OpenAPI query parameters whose underlying property is decorated with <see cref="JsonIgnoreAttribute"/>
/// (such as route-populated identifiers on query records).
/// </summary>
public sealed class JsonIgnoreParameterOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is not { Count: > 0 } parameters)
        {
            return Task.CompletedTask;
        }

        var ignoredNames = context.Description.ParameterDescriptions
            .Where(d => d.Source == BindingSource.Query && IsJsonIgnored(d.ModelMetadata))
            .Select(d => d.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ignoredNames.Count > 0)
        {
            for (int i = parameters.Count - 1; i >= 0; i--)
            {
                if (parameters[i].In == ParameterLocation.Query &&
                    parameters[i].Name is { } name &&
                    ignoredNames.Contains(name))
                {
                    parameters.RemoveAt(i);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsJsonIgnored(ModelMetadata? metadata)
        => metadata?.ContainerType is { } container &&
           metadata.PropertyName is { } propertyName &&
           container.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
               ?.IsDefined(typeof(JsonIgnoreAttribute), inherit: true) == true;
}
