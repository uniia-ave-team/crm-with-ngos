using System.Globalization;
using System.Linq.Expressions;
using Crm.Domain.Consts;

namespace Crm.Infrastructure.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> source, string? propertyName, string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return source;
        }

        var propertyInfo = typeof(T).GetProperties()
            .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

        if (propertyInfo == null)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = sortOrder?.ToLower(CultureInfo.InvariantCulture) == SortOrderConstants.Descending ? "OrderByDescending" : "OrderBy";

        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), propertyInfo.PropertyType],
            source.Expression,
            Expression.Quote(lambda));

        return source.Provider.CreateQuery<T>(resultExpression);
    }
}
