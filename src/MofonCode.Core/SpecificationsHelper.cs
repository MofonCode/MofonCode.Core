using System.Linq.Expressions;

namespace MofonCode.Core;

/// <summary>Combinators for composing LINQ expression predicates (And, Or, Not, All).</summary>
/// <remarks>
/// <para><b>Carried across from MarketSense with a fix.</b> The original built
/// <see cref="And{T}"/> and <see cref="Or{T}"/> with <see cref="Expression.Invoke(Expression, Expression[])"/>,
/// which produces an <see cref="InvocationExpression"/> node. LINQ-to-Objects evaluates that
/// happily, so the defect was invisible in memory — but EF Core's relational translator rejects
/// it, and a composed specification threw
/// <c>The LINQ expression could not be translated</c> at query time rather than at compile time.</para>
/// <para>Both combinators now rebind the right-hand lambda's parameter onto the left-hand one
/// with <see cref="ParameterRebinder"/> and combine the bodies directly, so the result is a
/// single lambda with a single parameter and no invocation node. That form translates.</para>
/// </remarks>
public static class SpecificationsHelper
{
    /// <summary>Returns a predicate that matches all elements.</summary>
    public static Expression<Func<T, bool>> All<T>() => x => true;

    /// <summary>Replaces the current predicate with one that matches all elements.</summary>
    public static Expression<Func<T, bool>> All<T>(this Expression<Func<T, bool>> _) => x => true;

    /// <summary>Negates the given predicate expression.</summary>
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters.Single());
    }

    /// <summary>Combines two predicate expressions with a logical AND.</summary>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        => combine(left, right, Expression.AndAlso);

    /// <summary>Combines two predicate expressions with a logical OR.</summary>
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        => combine(left, right, Expression.OrElse);

    private static Expression<Func<T, bool>> combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        ParameterExpression parameter = left.Parameters[0];

        // Rewrite every occurrence of the right lambda's parameter to the left lambda's, so both
        // bodies speak about the same parameter and can be merged without an invocation node.
        Expression rightBody = ParameterRebinder.Replace(right.Parameters[0], parameter, right.Body);

        return Expression.Lambda<Func<T, bool>>(merge(left.Body, rightBody), parameter);
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        public static Expression Replace(ParameterExpression from, ParameterExpression to, Expression body)
            => new ParameterRebinder(from, to).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
