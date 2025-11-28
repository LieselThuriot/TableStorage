using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for basic query operations including filtering, counting, and finding entities
/// Covers both table storage and blob storage query operations
/// </summary>
public class QueryTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region Table Storage Query Tests

    [Fact]
    public async Task Table_ToListAsync_ShouldReturnAllModels()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        });

        // Act
        List<Model2> models = await Context.Models2.ToListAsync();

        // Assert
        Assert.NotEmpty(models);
        Assert.True(models.Count >= 2);
    }

    [Fact]
    public async Task Table_FindAsync_ShouldReturnRequestedEntities()
    {
        // Arrange
        Model2 model1 = new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        };
        Model2 model2 = new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        };

        await Context.Models2.UpsertEntityAsync(model1);
        await Context.Models2.UpsertEntityAsync(model2);

        // Act
        List<Model2> findResults = await Context.Models2
            .FindAsync((model1.PartitionKey, model1.PrettyRow), (model2.PartitionKey, model2.PrettyRow))
            .ToListAsync();

        // Assert
        Assert.Equal(2, findResults.Count);
    }

    [Fact]
    public async Task Table_Where_WithEnumFilters_ShouldFilterCorrectly()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test",
            MyProperty7 = ModelEnum.Yes,
            MyProperty8 = ModelEnum.No
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test2",
            MyProperty7 = ModelEnum.No,
            MyProperty8 = ModelEnum.Yes
        });

        // Act
        List<Model2> enumFilters = await Context.Models2
            .Where(x => x.MyProperty7 == ModelEnum.Yes && x.MyProperty8 == ModelEnum.No)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(enumFilters);
        Assert.All(enumFilters, x =>
        {
            Assert.Equal(ModelEnum.Yes, x.MyProperty7);
            Assert.Equal(ModelEnum.No, x.MyProperty8);
        });
    }

    [Fact]
    public async Task Table_CountAsync_ShouldMatchListCount()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test"
        });

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test2"
        });

        // Act
        List<Model> proxiedList = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow != "")
            .ToListAsync();
        int proxyWorksCount = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow != "")
            .CountAsync();

        // Assert
        Assert.Equal(proxiedList.Count, proxyWorksCount);
    }

    [Fact]
    public async Task Table_Where_WithVariableValue_ShouldWork()
    {
        // Arrange
        var prettyItem = new { PrettyRow = "test-value" };
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = prettyItem.PrettyRow,
            MyProperty1 = 1,
            MyProperty2 = "test"
        });

        // Act
        List<Model2> visitorWorks = await Context.Models2
            .Where(x => x.PrettyRow == prettyItem.PrettyRow)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(visitorWorks);
        Assert.Equal(prettyItem.PrettyRow, visitorWorks[0].PrettyRow);
    }

    [Fact]
    public async Task Table_Where_WithNotEquals_ShouldWork()
    {
        // Arrange
        var prettyItem = new { PrettyRow = "test-value" };
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = prettyItem.PrettyRow,
            MyProperty1 = 1,
            MyProperty2 = "test"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = "different",
            MyProperty1 = 2,
            MyProperty2 = "test2"
        });

        // Act
        List<Model2> visitorWorks = await Context.Models2
            .Where(x => x.PrettyRow != prettyItem.PrettyRow)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(visitorWorks);
        Assert.NotEqual(prettyItem.PrettyRow, visitorWorks[0].PrettyRow);
    }

    [Fact]
    public async Task Table_SingleAsync_WithMultipleResults_ShouldThrowInvalidOperationException()
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Context.Models2
                .Where(x => x.PartitionKey == "root")
                .Where(x => x.MyProperty1 > 2)
                .SelectFields(x => x.MyProperty2)
                .SingleAsync();
        });
    }

    #endregion

    #region Blob Storage Query Tests

    [Fact]
    public async Task Blob_FindAsync_WithMultipleKeys_ShouldReturnAllEntities()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId1 = Guid.NewGuid().ToString("N");
        string blobId2 = Guid.NewGuid().ToString("N");

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId1,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId2,
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        });

        // Act
        List<Model4> findBlobResults = await Context.Models4Blob
            .FindAsync(("root", blobId1), ("root", blobId2))
            .ToListAsync();

        // Assert
        Assert.Equal(2, findBlobResults.Count);
    }

    [Fact]
    public async Task Blob_FindAsync_WithSingleKey_ShouldReturnEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId1 = Guid.NewGuid().ToString("N");

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId1,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        // Act
        Model4? findSingleBlobResult = await Context.Models4Blob.FindAsync("root", blobId1);

        // Assert
        Assert.NotNull(findSingleBlobResult);
    }

    [Fact]
    public async Task Blob_GetEntityOrDefaultAsync_WithExistingEntity_ShouldReturnEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        // Act
        Model4? blob = await Context.Models4Blob.GetEntityOrDefaultAsync("root", blobId);

        // Assert
        Assert.NotNull(blob);
        Assert.Equal(1, blob.MyProperty1);
        Assert.Equal("test 1", blob.MyProperty2);
    }

    [Fact]
    public async Task Blob_GetEntityOrDefaultAsync_WithNonExistingEntity_ShouldReturnNull()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");

        // Act
        Model4? blob = await Context.Models4Blob.GetEntityOrDefaultAsync("root", Guid.NewGuid().ToString("N"));

        // Assert
        Assert.Null(blob);
    }

    [Fact]
    public async Task Blob_Where_WithComplexFilter_ShouldFilterByTagsAndProperties()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 2,
            MyProperty2 = "test value"
        });

        // Act
        List<Model4> blobResult = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root")
            .Where(x => x.PrettyRow == blobId)
            .Where(x => x.MyProperty1 == 2)
            .Where(x => x.MyProperty2 == "test value")
            .ToListAsync();

        // Assert
        Assert.Single(blobResult);
        Assert.Equal(2, blobResult[0].MyProperty1);
        Assert.Equal("test value", blobResult[0].MyProperty2);
    }

    [Fact]
    public async Task Blob_Where_WithTagsOnly_ShouldFilterByTags()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 2,
            MyProperty2 = "test value"
        });

        // Act
        List<Model4> blobResult = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root")
            .Where(x => x.PrettyRow == blobId)
            .Where(x => x.MyProperty1 == 2)
            .ToListAsync();

        // Assert
        Assert.Single(blobResult);
        Assert.Equal(2, blobResult[0].MyProperty1);
        Assert.Equal("test value", blobResult[0].MyProperty2);
    }

    [Fact]
    public async Task Blob_Where_WithPartitionAndRowKey_ShouldReturnEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 2,
            MyProperty2 = "test value"
        });

        // Act
        List<Model4> blobResult = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root" && x.PrettyRow == blobId)
            .ToListAsync();

        // Assert
        Assert.Single(blobResult);
        Assert.Equal(2, blobResult[0].MyProperty1);
        Assert.Equal("test value", blobResult[0].MyProperty2);
    }

    #endregion
}