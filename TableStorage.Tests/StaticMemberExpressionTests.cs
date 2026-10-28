using System.Linq.Expressions;
using Azure.Data.Tables;
using TableStorage.Tests.Models;
using TableStorage.Visitors;

namespace TableStorage.Tests;

/// <summary>
/// Validates that the expression visitors evaluate static members, captured locals and nested member chains instead of failing on them.
/// </summary>
public class StaticMemberExpressionTests
{
    // Intentionally static fields rather than constants, as constants are inlined and would not reproduce the issue
#pragma warning disable RCS1187 // Use constant instead of field
    private static readonly string s_partition = "static-partition";
    private static readonly TimeSpan s_margin = TimeSpan.FromMinutes(1);
    private static string StaticRow { get; } = "static-row";
    private const int ConstantNumber = 42;
    private static readonly int s_number = 7;
#pragma warning restore RCS1187 // Use constant instead of field

    private sealed class Holder
    {
        public static Holder Instance { get; } = new();
        public string Value { get; } = "nested-value";
    }

    private static readonly ModelInfo s_modelInfo = new("PrettyName", "PrettyRow", typeof(Model));

    #region ExpressionEvaluator

    [Fact]
    public void ExpressionEvaluator_StaticField_IsEvaluated()
    {
        Expression<Func<string>> expression = () => s_partition;

        Assert.True(ExpressionEvaluator.TryGetValue(expression.Body, out object? value));
        Assert.Equal(s_partition, value);
    }

    [Fact]
    public void ExpressionEvaluator_StaticProperty_IsEvaluated()
    {
        Expression<Func<string>> expression = () => StaticRow;

        Assert.True(ExpressionEvaluator.TryGetValue(expression.Body, out object? value));
        Assert.Equal(StaticRow, value);
    }

    [Fact]
    public void ExpressionEvaluator_NestedStaticChain_IsEvaluated()
    {
        Expression<Func<string>> expression = () => Holder.Instance.Value;

        Assert.True(ExpressionEvaluator.TryGetValue(expression.Body, out object? value));
        Assert.Equal("nested-value", value);
    }

    [Fact]
    public void ExpressionEvaluator_NestedCapturedChain_IsEvaluated()
    {
        Holder holder = new();
        Expression<Func<string>> expression = () => holder.Value;

        Assert.True(ExpressionEvaluator.TryGetValue(expression.Body, out object? value));
        Assert.Equal("nested-value", value);
    }

    [Fact]
    public void ExpressionEvaluator_ParameterMember_IsNotEvaluated()
    {
        Expression<Func<TableEntity, string>> expression = x => x.PartitionKey;

        Assert.False(ExpressionEvaluator.TryGetValue(expression.Body, out _));
    }

    [Fact]
    public void ExpressionEvaluator_KeepsDeclaredType()
    {
        Expression<Func<TimeSpan>> expression = () => s_margin;

        Assert.True(ExpressionEvaluator.TryEvaluateMember((MemberExpression)expression.Body, out ConstantExpression constant));
        Assert.Equal(typeof(TimeSpan), constant.Type);
    }

    #endregion ExpressionEvaluator

    #region WhereVisitor

    [Fact]
    public void WhereVisitor_StaticFieldComparison_IsEvaluated()
    {
        Expression<Func<Model, bool>> filter = x => x.PrettyName == s_partition && x.PrettyRow == StaticRow;

        var result = (Expression<Func<Model, bool>>)new WhereVisitor(s_modelInfo).Visit(filter);

        Assert.Equal("""((Convert(x, ITableEntity).PartitionKey == "static-partition") AndAlso (Convert(x, ITableEntity).RowKey == "static-row"))""", result.Body.ToString());
    }

    [Fact]
    public void WhereVisitor_ArithmeticWithStaticMember_DoesNotThrow()
    {
        DateTimeOffset since = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Model, bool>> filter = x => x.Timestamp > since - s_margin;

        var result = (Expression<Func<Model, bool>>)new WhereVisitor(s_modelInfo).Visit(filter);
        Func<Model, bool> compiled = result.Compile();

        Assert.DoesNotContain(nameof(s_margin), result.Body.ToString());
        Assert.True(compiled(new Model { Timestamp = since }));
        Assert.False(compiled(new Model { Timestamp = since.AddMinutes(-2) }));
    }

    [Fact]
    public void WhereVisitor_NestedMembers_AreEvaluated()
    {
        Holder holder = new();
        Expression<Func<Model, bool>> filter = x => x.MyProperty2 == holder.Value || x.MyProperty2 == Holder.Instance.Value;

        var result = (Expression<Func<Model, bool>>)new WhereVisitor(s_modelInfo).Visit(filter);

        Assert.Equal("""((x.MyProperty2 == "nested-value") OrElse (x.MyProperty2 == "nested-value"))""", result.Body.ToString());
    }

    [Fact]
    public void WhereVisitor_ConstantAndStaticNumbers_AreEvaluated()
    {
        Expression<Func<Model, bool>> filter = x => x.MyProperty1 > ConstantNumber || x.MyProperty1 < s_number;

        var result = (Expression<Func<Model, bool>>)new WhereVisitor(s_modelInfo).Visit(filter);

        Assert.Equal("((x.MyProperty1 > 42) OrElse (x.MyProperty1 < 7))", result.Body.ToString());
    }

    #endregion WhereVisitor

    #region PartitionKeySplitVisitor

    [Fact]
    public void PartitionKeySplit_StaticPartitionKeys_AreSplit()
    {
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == s_partition || x.PartitionKey == Holder.Instance.Value;

        var result = PartitionKeySplitVisitor.TrySplit(filter);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    #endregion PartitionKeySplitVisitor

    #region BlobQueryVisitor

    [Fact]
    public void BlobQueryVisitor_StaticMembers_AreUsedInFilter()
    {
        Expression<Func<Model, bool>> filter = x => x.PrettyName == s_partition && x.PrettyRow == Holder.Instance.Value;

        BlobQueryVisitor visitor = new(s_modelInfo, []);
        _ = visitor.Visit(filter);

        Assert.False(visitor.Error);
        Assert.Equal("partition = 'static-partition' and row = 'nested-value'", visitor.Filter);
    }

    [Fact]
    public void BlobQueryVisitor_StaticMemberOnLeft_DoesNotThrow()
    {
        Expression<Func<Model, bool>> filter = x => s_partition == x.PrettyName;

        BlobQueryVisitor visitor = new(s_modelInfo, []);
        _ = visitor.Visit(filter);

        Assert.False(visitor.Error);
        Assert.Equal("partition = 'static-partition'", visitor.Filter);
    }

    #endregion BlobQueryVisitor

    #region BlobTagQueryVisitor

    [Fact]
    public void BlobTagQueryVisitor_StaticMember_IsNotDropped()
    {
        Expression<Func<Model, bool>> filter = x => x.MyProperty2 == s_partition;

        var result = (LambdaExpression)new BlobTagQueryVisitor<Model>(s_modelInfo, ["MyProperty2"]).Visit(filter);
        Delegate compiled = result.Compile();

        Assert.True((bool)compiled.DynamicInvoke(new BlobTagAccessor(new Dictionary<string, string> { ["MyProperty2"] = s_partition }))!);
        Assert.False((bool)compiled.DynamicInvoke(new BlobTagAccessor(new Dictionary<string, string> { ["MyProperty2"] = "other" }))!);
    }

    [Fact]
    public void BlobTagQueryVisitor_NestedMember_IsNotDropped()
    {
        Expression<Func<Model, bool>> filter = x => x.PrettyName == Holder.Instance.Value;

        var result = (LambdaExpression)new BlobTagQueryVisitor<Model>(s_modelInfo, []).Visit(filter);
        Delegate compiled = result.Compile();

        Assert.True((bool)compiled.DynamicInvoke(new BlobTagAccessor(new Dictionary<string, string> { ["partition"] = "nested-value" }))!);
        Assert.False((bool)compiled.DynamicInvoke(new BlobTagAccessor(new Dictionary<string, string> { ["partition"] = "other" }))!);
    }

    #endregion BlobTagQueryVisitor

    #region MergeVisitor

    [Fact]
    public void MergeVisitor_StaticMembers_AreEvaluated()
    {
        Holder holder = new();
        Expression<Func<Model>> merge = () => new Model
        {
            PrettyName = s_partition,
            PrettyRow = StaticRow,
            MyProperty1 = s_number,
            MyProperty2 = holder.Value,
            MyProperty9 = Holder.Instance.Value,
            MyProperty4 = null,
        };

        MergeVisitor visitor = new(s_modelInfo, _ => s_modelInfo);
        _ = visitor.Visit(merge);

        Assert.False(visitor.IsComplex);
        Assert.Equal(s_partition, visitor.Entity.PartitionKey);
        Assert.Equal(StaticRow, visitor.Entity.RowKey);
        Assert.Equal(s_number, visitor.Entity["MyProperty1"]);
        Assert.Equal("nested-value", visitor.Entity["MyProperty2"]);
        Assert.Equal("nested-value", visitor.Entity["MyProperty9"]);
        Assert.Null(visitor.Entity["MyProperty4"]);
    }

    #endregion MergeVisitor
}
