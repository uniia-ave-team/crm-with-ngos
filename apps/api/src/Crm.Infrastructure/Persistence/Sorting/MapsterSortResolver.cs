using System.Linq.Expressions;
using System.Reflection;
using Crm.Domain.Extensions;
using Mapster;
using Mapster.Models;

namespace Crm.Infrastructure.Persistence.Sorting;

/// <summary>
/// Resolves DTO property names to Entity navigation paths by parsing Mapster projection expressions.
/// This allows dynamic LINQ sorting on DTO properties (e.g., <c>"Email"</c>) to be translated
/// to database entity paths (e.g., <c>"UserProfile.FirstName"</c>) for Entity Framework Core.
/// </summary>
public static class MapsterSortResolver
{
    /// <summary>
    /// Generates a dictionary mapping scalar DTO property names to their corresponding scalar Entity paths.
    /// Non-scalar and collection properties are excluded so dynamic LINQ ordering never targets collection navigations.
    /// </summary>
    /// <typeparam name="TEntity">The source database entity type.</typeparam>
    /// <typeparam name="TDto">The destination data transfer object type.</typeparam>
    /// <returns>A case-insensitive dictionary where the key is the DTO property and the value is the Entity path.</returns>
    public static Dictionary<string, string> GetSortMapping<TEntity, TDto>()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ExtractMapsterPaths<TEntity, TDto>(map);
        ApplyFallbackProperties<TEntity, TDto>(map);

        return map;
    }

    private static void ExtractMapsterPaths<TEntity, TDto>(Dictionary<string, string> map)
    {
        try
        {
            var typeTuple = new TypeTuple(typeof(TEntity), typeof(TDto));

            var mapExpression = TypeAdapterConfig.GlobalSettings.CreateMapExpression(typeTuple, MapType.Projection);
            if (mapExpression == null)
            {
                return;
            }

            var parameter = mapExpression.Parameters[0];

            if (mapExpression.Body is MemberInitExpression memberInit)
            {
                foreach (var assignment in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    if (assignment.Member is PropertyInfo prop && !prop.PropertyType.IsScalarType())
                    {
                        continue;
                    }

                    var visitor = new PropertyPathVisitor(parameter);
                    visitor.Visit(assignment.Expression);

                    if (!string.IsNullOrEmpty(visitor.ResolvedPath))
                    {
                        map[assignment.Member.Name] = visitor.ResolvedPath;
                    }
                }
            }
            else if (mapExpression.Body is NewExpression { Constructor: not null } newExpr)
            {
                foreach (var (param, arg) in newExpr.Constructor.GetParameters().Zip(newExpr.Arguments))
                {
                    if (string.IsNullOrEmpty(param.Name) || !param.ParameterType.IsScalarType())
                    {
                        continue;
                    }

                    var visitor = new PropertyPathVisitor(parameter);
                    visitor.Visit(arg);

                    if (!string.IsNullOrEmpty(visitor.ResolvedPath))
                    {
                        map[param.Name] = visitor.ResolvedPath;
                    }
                }
            }
        }
        catch
        {
            // Fallback to basic reflection if Mapster expression generation fails.
        }
    }

    private static void ApplyFallbackProperties<TEntity, TDto>(Dictionary<string, string> map)
    {
        var entityProps = typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsScalarType())
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dtoProps = typeof(TDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsScalarType());

        foreach (var propName in dtoProps.Select(p => p.Name).Where(entityProps.Contains))
        {
            map.TryAdd(propName, propName);
        }
    }

    private sealed class PropertyPathVisitor(ParameterExpression rootParameter) : ExpressionVisitor
    {
        public string? ResolvedPath { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Do not descend into LINQ / aggregate method calls (e.g., src.Attachments.Count() or src.Fields.OrderBy(...))
            // so collection navigation roots are never misidentified as direct scalar member paths.
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (!node.Type.IsScalarType())
            {
                return base.VisitMember(node);
            }

            var path = new List<string>();
            Expression? current = node;

            while (current is MemberExpression memberExpr)
            {
                path.Add(memberExpr.Member.Name);
                current = memberExpr.Expression;
            }

            if (current == rootParameter)
            {
                path.Reverse();
                var fullPath = string.Join(".", path);

                if (ResolvedPath == null || fullPath.Length > ResolvedPath.Length)
                {
                    ResolvedPath = fullPath;
                }
            }

            return base.VisitMember(node);
        }
    }
}
