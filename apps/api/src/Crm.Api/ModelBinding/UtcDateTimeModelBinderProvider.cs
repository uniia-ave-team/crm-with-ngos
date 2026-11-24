using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crm.Api.ModelBinding;

/// <summary>
/// Provides <see cref="UtcDateTimeModelBinder"/> for all <see cref="DateTime"/> and <see cref="DateTime"/>? parameters
/// bound from query strings, routes, or forms.
/// </summary>
public sealed class UtcDateTimeModelBinderProvider : IModelBinderProvider
{
    private static readonly UtcDateTimeModelBinder Binder = new();

    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.UnderlyingOrModelType == typeof(DateTime)
            ? Binder
            : null;
    }
}
