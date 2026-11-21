using Crm.Api.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crm.Api.ModelBinding;

/// <summary>
/// Binds <see cref="DateTime"/> and <see cref="DateTime"/>? query, route, and form parameters into UTC (<see cref="DateTimeKind.Utc"/>).
/// </summary>
public sealed class UtcDateTimeModelBinder : IModelBinder
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        string modelName = bindingContext.ModelName;
        ValueProviderResult valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        string? raw = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }

            return Task.CompletedTask;
        }

        if (UtcDateTimeParser.TryParseUtc(raw, out DateTime parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(modelName, FormatInvalidValueMessage(raw));
        }

        return Task.CompletedTask;
    }

    private static string FormatInvalidValueMessage(string raw)
        => $"The value '{raw}' is not valid.";
}
