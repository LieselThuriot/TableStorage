using System.Linq.Expressions;
using Azure.Data.Tables;
using TableStorage.Fluent;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

public class DollarTypeFilterTests
{
    private static Expression<Func<T, bool>> CreateTypeFilter<T>(string typeName)
    {
        return Expression.Lambda<Func<T, bool>>(
            Expression.Equal(DollarTypeClass.TypeProperty, Expression.Constant(typeName)),
            Expression.Parameter(typeof(T), "x"));
    }

    [Fact]
    public void DollarTypeProperty_TranslatesToTypeDiscriminatorFilter()
    {
        string query = TableClient.CreateQueryFilter(CreateTypeFilter<TableEntity>("FluentTestModelA"));

        Assert.Equal("$type eq 'FluentTestModelA'", query);
    }

#if !PublishAot
    [Fact]
    public void DollarTypeProperty_RuntimeCompilation_ReadsTypeDiscriminator()
    {
        FluentRuntimeCompilationFactory factory = new(new CompilationFactory());
        Func<FluentTableEntity<FluentTestModelA, FluentTestModelB>, bool> filter = factory.Compile(CreateTypeFilter<FluentTableEntity<FluentTestModelA, FluentTestModelB>>("FluentTestModelA"));

        FluentTableEntity<FluentTestModelA, FluentTestModelB> modelA = new FluentTestModelA { PrettyPartitionA = "p", PrettyRowA = "a", TypeA = "A", PropertyA = 1 };
        FluentTableEntity<FluentTestModelA, FluentTestModelB> modelB = new FluentTestModelB { PrettyPartitionB = "p", PrettyRowB = "b", TypeB = "B", PropertyB = true };

        Assert.True(filter(modelA));
        Assert.False(filter(modelB));
    }

    [Fact]
    public void DollarTypeProperty_RuntimeCompilation_CombinedWithModelProperties()
    {
        Expression<Func<FluentTableEntity<FluentTestModelA, FluentTestModelB>, bool>> typeFilter = CreateTypeFilter<FluentTableEntity<FluentTestModelA, FluentTestModelB>>("FluentTestModelB");
        ParameterExpression parameter = typeFilter.Parameters[0];
        Expression<Func<FluentTableEntity<FluentTestModelA, FluentTestModelB>, bool>> filter = Expression.Lambda<Func<FluentTableEntity<FluentTestModelA, FluentTestModelB>, bool>>(
            Expression.AndAlso(typeFilter.Body, Expression.Equal(Expression.Property(parameter, nameof(ITableEntity.PartitionKey)), Expression.Constant("p"))),
            parameter);

        Func<FluentTableEntity<FluentTestModelA, FluentTestModelB>, bool> compiled = new FluentRuntimeCompilationFactory(new CompilationFactory()).Compile(filter);

        Assert.True(compiled(new FluentTestModelB { PrettyPartitionB = "p", PrettyRowB = "b", TypeB = "B", PropertyB = true }));
        Assert.False(compiled(new FluentTestModelB { PrettyPartitionB = "other", PrettyRowB = "b", TypeB = "B", PropertyB = true }));
        Assert.False(compiled(new FluentTestModelA { PrettyPartitionA = "p", PrettyRowA = "a", TypeA = "A", PropertyA = 1 }));
    }
#endif
}

#if !PublishAot
/// <summary>
/// Runs the generated $type filter together with runtime compiled (FastExpressionCompiler) projections against Azurite.
/// </summary>
public class DollarTypeRuntimeCompilationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task WhereFirstType_WithCompiledSelect_OnlyReturnsFirstType()
    {
        string partition = Guid.NewGuid().ToString("N");
        await Context.FluentModels.UpsertEntityAsync(new FluentTestModelA { PrettyPartitionA = partition, PrettyRowA = "a", TypeA = "A", PropertyA = 7 }, TestContext.Current.CancellationToken);
        await Context.FluentModels.UpsertEntityAsync(new FluentTestModelB { PrettyPartitionB = partition, PrettyRowB = "b", TypeB = "B", PropertyB = true }, TestContext.Current.CancellationToken);

        List<string> results = await Context.FluentModels
            .WhereFirstType()
            .Where(x => x.PrettyPartitionA == partition)
            .Select(x => x.TypeA + x.PropertyA)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["A7"], results);
    }

    [Fact]
    public async Task WhereSecondType_WithCompiledSelect_OnlyReturnsSecondType()
    {
        string partition = Guid.NewGuid().ToString("N");
        await Context.FluentModels.UpsertEntityAsync(new FluentTestModelA { PrettyPartitionA = partition, PrettyRowA = "a", TypeA = "A", PropertyA = 7 }, TestContext.Current.CancellationToken);
        await Context.FluentModels.UpsertEntityAsync(new FluentTestModelB { PrettyPartitionB = partition, PrettyRowB = "b", TypeB = "B", PropertyB = true }, TestContext.Current.CancellationToken);

        List<string> results = await Context.FluentModels
            .WhereSecondType()
            .Where(x => x.PrettyPartitionB == partition)
            .Select(x => x.TypeB + x.PropertyB)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["BTrue"], results);
    }

    [Fact]
    public async Task WhereFirstType_WithBatchUpdate_OnlyUpdatesFirstType()
    {
        string partition = Guid.NewGuid().ToString("N");
        await Context.FluentModels.UpsertEntityAsync(new FluentTestModelA { PrettyPartitionA = partition, PrettyRowA = "a", TypeA = "A", PropertyA = 1 }, TestContext.Current.CancellationToken);
        await Context.FluentModels.UpsertEntityAsync(new FluentTestModelB { PrettyPartitionB = partition, PrettyRowB = "b", TypeB = "B", PropertyB = false }, TestContext.Current.CancellationToken);

        int updated = await Context.FluentModels
            .WhereFirstType()
            .Where(x => x.PrettyPartitionA == partition)
            .BatchUpdateAsync(x => new() { PropertyA = x.PropertyA + 1 }, TestContext.Current.CancellationToken);

        FluentTableEntity<FluentTestModelA, FluentTestModelB>? a = await Context.FluentModels.FindAsync(partition, "a", TestContext.Current.CancellationToken);
        FluentTableEntity<FluentTestModelA, FluentTestModelB>? b = await Context.FluentModels.FindAsync(partition, "b", TestContext.Current.CancellationToken);

        Assert.Equal(1, updated);
        Assert.Equal(2, a?["PropertyA"]);
        Assert.Equal(false, b?["PropertyB"]);
    }
}
#endif
