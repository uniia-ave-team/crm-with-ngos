using System.Linq.Expressions;
using System.Reflection;
using Mapster;
using Mapster.Models;

namespace Crm.Infrastructure.Helpers;

/// <summary>
/// Resolves DTO property names to Entity navigation paths by parsing Mapster projection expressions.
/// This allows dynamic LINQ sorting on DTO properties (e.g., "Email") to be correctly translated
/// to database entity paths (e.g., "AuthUser.Email") for Entity Framework Core.
/// </summary>
public static class MapsterSortResolver
{
    /// <summary>
    /// Generates a dictionary mapping DTO property names to their corresponding Entity paths.
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

    /// <summary>
    /// Extracts complex mappings (like nested properties or coalescing operators) directly from
    /// Mapster's generated expression tree (Projection configuration).
    /// </summary>
    /// <typeparam name="TEntity">The source entity type.</typeparam>
    /// <typeparam name="TDto">The destination DTO type.</typeparam>
    /// <param name="map">The dictionary to populate with resolved paths.</param>
    private static void ExtractMapsterPaths<TEntity, TDto>(Dictionary<string, string> map)
    {
        try
        {
            var typeTuple = new TypeTuple(typeof(TEntity), typeof(TDto));

            // Creates a lambda expression representing the projection
            var mapExpression = TypeAdapterConfig.GlobalSettings.CreateMapExpression(typeTuple, MapType.Projection);
            if (mapExpression == null)
            {
                return;
            }

            var parameter = mapExpression.Parameters[0]; // Represents the root 'src' entity parameter

            // 1. Handle standard object initialization: new Dto { Prop = src.Prop }
            if (mapExpression.Body is MemberInitExpression memberInit)
            {
                foreach (var assignment in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    var visitor = new PropertyPathVisitor(parameter);
                    visitor.Visit(assignment.Expression);

                    if (!string.IsNullOrEmpty(visitor.ResolvedPath))
                    {
                        map[assignment.Member.Name] = visitor.ResolvedPath;
                    }
                }
            }

            // 2. Handle C# records and constructors: new Dto(src.Prop1, src.Prop2)
            else if (mapExpression.Body is NewExpression { Constructor: not null } newExpr)
            {
                foreach (var (param, arg) in newExpr.Constructor.GetParameters().Zip(newExpr.Arguments))
                {
                    if (string.IsNullOrEmpty(param.Name))
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
            // Ensures the application doesn't crash during mapping initialization.
        }
    }

    /// <summary>
    /// Populates the dictionary with 1-to-1 property mappings (e.g., "FirstName" -> "FirstName")
    /// for properties that exist in both the Entity and the DTO and weren't overridden by Mapster.
    /// </summary>
    private static void ApplyFallbackProperties<TEntity, TDto>(Dictionary<string, string> map)
    {
        var entityProps = typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dtoProps = typeof(TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var propName in dtoProps.Select(p => p.Name).Where(entityProps.Contains))
        {
            map.TryAdd(propName, propName);
        }
    }

    /// <summary>
    /// Custom ExpressionVisitor designed to walk down a property access chain
    /// and extract the longest valid navigation path originating from the root parameter.
    /// </summary>
    private sealed class PropertyPathVisitor(ParameterExpression rootParameter) : ExpressionVisitor
    {
        /// <summary>
        /// Gets the final string representation of the navigation path (e.g., "AuthUser.Email").
        /// </summary>
        public string? ResolvedPath { get; private set; }

        /// <summary>
        /// Visits member access expressions (like src.AuthUser.Email) and reconstructs the path.
        /// </summary>
        protected override Expression VisitMember(MemberExpression node)
        {
            var path = new List<string>();
            Expression? current = node;

            // Traverse upwards from the leaf node (e.g., Email -> AuthUser -> src)
            while (current is MemberExpression memberExpr)
            {
                path.Add(memberExpr.Member.Name);
                current = memberExpr.Expression;
            }

            // Verify the chain originates from the actual entity parameter ('src')
            if (current == rootParameter)
            {
                path.Reverse();
                var fullPath = string.Join(".", path);

                // Ensure we capture the deepest path (useful for expressions like 'src.AuthUser.Email ?? ""')
                if (ResolvedPath == null || fullPath.Length > ResolvedPath.Length)
                {
                    ResolvedPath = fullPath;
                }
            }

            return base.VisitMember(node);
        }
    }
}
