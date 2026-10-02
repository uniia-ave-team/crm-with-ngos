using System.Net.Mime;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Crm.Api.OpenApi.Transformers;

/// <summary>
/// Transformer that documents only <c>application/json</c> where the JSON formatters also advertise
/// <c>text/plain</c>, <c>text/json</c> or <c>application/*+json</c>.
/// The API still accepts those media types; the document lists one so generated clients get one function per operation.
/// </summary>
public class JsonContentTypeOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] RedundantMediaTypes = [MediaTypeNames.Text.Plain, "text/json", "application/*+json"];

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        RemoveRedundantMediaTypes(operation.RequestBody?.Content);

        if (operation.Responses is not null)
        {
            foreach (var response in operation.Responses.Values)
            {
                RemoveRedundantMediaTypes(response.Content);
            }
        }

        return Task.CompletedTask;
    }

    private static void RemoveRedundantMediaTypes<TMediaType>(IDictionary<string, TMediaType>? content)
    {
        if (content is null || !content.ContainsKey(MediaTypeNames.Application.Json))
        {
            return;
        }

        foreach (string mediaType in RedundantMediaTypes)
        {
            content.Remove(mediaType);
        }
    }
}
