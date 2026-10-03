using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Crm.Api.OpenApi.Transformers;

/// <summary>
/// Transformer that sets a stable operation ID in the form <c>{Controller}{Action}</c>, e.g. <c>UsersAssignRole</c>.
/// Generated API clients use operation IDs as function names.
/// </summary>
public class OperationIdOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor action)
        {
            operation.OperationId = $"{action.ControllerName}{action.ActionName}";
        }

        return Task.CompletedTask;
    }
}
