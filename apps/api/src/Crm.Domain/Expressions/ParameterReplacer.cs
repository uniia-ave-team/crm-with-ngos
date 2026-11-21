using System.Linq.Expressions;

namespace Crm.Domain.Expressions;

/// <summary>
/// Replaces all occurrences of a target <see cref="ParameterExpression"/> with a replacement <see cref="Expression"/>
/// so reusable domain expressions can be composed cleanly into SQL-translatable LINQ queries.
/// </summary>
/// <param name="source">The parameter expression to replace.</param>
/// <param name="replacement">The replacement expression.</param>
internal sealed class ParameterReplacer(ParameterExpression source, Expression replacement) : ExpressionVisitor
{
    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
        => node == source ? replacement : base.VisitParameter(node);
}
