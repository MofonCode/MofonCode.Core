using System.Linq.Expressions;

namespace MofonCode.Core.Tests;

/// <summary>
/// Covers the combinators, and in particular the defect carried over from MarketSense: And/Or
/// were built with Expression.Invoke, which evaluates fine in memory but cannot be translated
/// by a relational LINQ provider. The shape assertions below are the regression test - they
/// fail against the original implementation.
/// </summary>
[TestClass]
public sealed class SpecificationsHelperTests
{
    private sealed record Row(int Value, string Name);

    private static readonly Row[] _rows =
    [
        new(1, "one"),
        new(2, "two"),
        new(3, "three"),
        new(4, "four"),
    ];

    private sealed class InvocationDetector : ExpressionVisitor
    {
        private bool _found;

        public static bool HasInvocation(Expression expression)
        {
            InvocationDetector detector = new();
            detector.Visit(expression);
            return detector._found;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            _found = true;
            return node;
        }
    }

    [TestMethod]
    public void And_producesNoInvocationNode()
    {
        Expression<Func<Row, bool>> left = r => r.Value > 1;
        Expression<Func<Row, bool>> right = r => r.Value < 4;

        Expression<Func<Row, bool>> combined = left.And(right);

        Assert.IsFalse(
            InvocationDetector.HasInvocation(combined),
            "And must not emit an InvocationExpression; a relational provider cannot translate one.");
    }

    [TestMethod]
    public void Or_producesNoInvocationNode()
    {
        Expression<Func<Row, bool>> left = r => r.Value == 1;
        Expression<Func<Row, bool>> right = r => r.Value == 4;

        Expression<Func<Row, bool>> combined = left.Or(right);

        Assert.IsFalse(
            InvocationDetector.HasInvocation(combined),
            "Or must not emit an InvocationExpression; a relational provider cannot translate one.");
    }

    [TestMethod]
    public void And_collapsesToASingleParameter()
    {
        Expression<Func<Row, bool>> left = r => r.Value > 1;
        Expression<Func<Row, bool>> right = r => r.Name.Length > 3;

        Expression<Func<Row, bool>> combined = left.And(right);

        Assert.AreEqual(1, combined.Parameters.Count, "the combined lambda takes one parameter");
    }

    [TestMethod]
    public void And_matchesOnlyRowsSatisfyingBothSides()
    {
        Expression<Func<Row, bool>> left = r => r.Value > 1;
        Expression<Func<Row, bool>> right = r => r.Value < 4;

        int[] matched = [.. _rows.AsQueryable().Where(left.And(right)).Select(r => r.Value)];

        CollectionAssert.AreEqual(new[] { 2, 3 }, matched);
    }

    [TestMethod]
    public void Or_matchesRowsSatisfyingEitherSide()
    {
        Expression<Func<Row, bool>> left = r => r.Value == 1;
        Expression<Func<Row, bool>> right = r => r.Value == 4;

        int[] matched = [.. _rows.AsQueryable().Where(left.Or(right)).Select(r => r.Value)];

        CollectionAssert.AreEqual(new[] { 1, 4 }, matched);
    }

    [TestMethod]
    public void Not_invertsThePredicate()
    {
        Expression<Func<Row, bool>> predicate = r => r.Value > 2;

        int[] matched = [.. _rows.AsQueryable().Where(predicate.Not()).Select(r => r.Value)];

        CollectionAssert.AreEqual(new[] { 1, 2 }, matched);
    }

    [TestMethod]
    public void All_matchesEveryRow()
    {
        int[] matched = [.. _rows.AsQueryable().Where(SpecificationsHelper.All<Row>()).Select(r => r.Value)];

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, matched);
    }

    [TestMethod]
    public void And_chainsMoreThanTwoPredicatesWithoutInvocationNodes()
    {
        Expression<Func<Row, bool>> a = r => r.Value > 0;
        Expression<Func<Row, bool>> b = r => r.Value < 4;
        Expression<Func<Row, bool>> c = r => r.Name.StartsWith('t');

        Expression<Func<Row, bool>> combined = a.And(b).And(c);

        Assert.IsFalse(InvocationDetector.HasInvocation(combined));
        int[] matched = [.. _rows.AsQueryable().Where(combined).Select(r => r.Value)];
        CollectionAssert.AreEqual(new[] { 2, 3 }, matched);
    }

    [TestMethod]
    public void And_rejectsNullOperands()
    {
        Expression<Func<Row, bool>> left = r => r.Value > 1;

        Assert.ThrowsExactly<ArgumentNullException>(() => left.And(null!));
    }
}
