using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for batch operations including BatchUpdate and BatchDelete transactions
/// Covers both table storage batch operations
/// </summary>
public class BatchOperationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task BatchDeleteTransactionAsync_ShouldDeleteMatchingEntities()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test2"
        });

        // Act
        int fiveCount = await Context.Models2.Where(x => x.MyProperty1 == 5).CountAsync();
        int deleteCount = await Context.Models2.Where(x => x.MyProperty1 == 5).BatchDeleteTransactionAsync();
        List<Model2> newModels2 = await Context.Models2.ToListAsync();

        // Assert
        Assert.Equal(fiveCount, deleteCount);
    }

    [Fact]
    public async Task BatchUpdateTransactionAsync_ShouldUpdateMatchingEntities()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "original"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "original2"
        });

        // Act
        int updateCount = await Context.Models2
            .Where(x => x.MyProperty1 == 1)
            .BatchUpdateTransactionAsync(x => new() { MyProperty2 = "updated" });
        List<Model2> updatedModels = await Context.Models2
            .Where(x => x.MyProperty2 == "updated")
            .ToListAsync();

        // Assert
        Assert.Equal(updateCount, updatedModels.Count);
    }
}
