using Crm.Api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Crm.Api.OpenApi.Transformers;

/// <summary>
/// Transformer for native .NET OpenAPI generation that appends required access rights to the endpoint description.
/// </summary>
public class AccessRightOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var attributes = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<HasAccessRightAttribute>()
            .ToList();

        if (attributes.Count == 0)
        {
            return Task.CompletedTask;
        }

        var rights = attributes
            .Select(attr => attr.Policy)
            .Where(policy => !string.IsNullOrEmpty(policy))
            .Distinct()
            .ToList();

        if (rights.Count == 0)
        {
            return Task.CompletedTask;
        }

        var rightsText = string.Join(", ", rights.Select(r => $"`{r}`"));
        var accessInfo = $"\n\n> 🔒 **Required Permissions:** {rightsText}";

        operation.Description = string.IsNullOrEmpty(operation.Description)
            ? accessInfo.TrimStart()
            : operation.Description + accessInfo;

        return Task.CompletedTask;
    }
}
