#pragma warning disable IDE0065 // Misplaced using directive
#pragma warning disable CA1050 // Declare types in namespaces
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CS0162 // Unreachable code detected

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TableStorage;
using TableStorage.Linq;
using TableStorage.Tests.Contexts;
using TableStorage.Tests.Models;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", true)
    .AddUserSecrets("67f67690-8436-4eea-9077-95dcc941c6db")
    .Build();

ServiceCollection services = new();

string? connectionString = config.GetConnectionString("Storage");
ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

#if PublishAot
const bool create = false;
#else
const bool create = true;
#endif

services.AddMyTableContext(connectionString,
    configure: x =>
    {
        x.CreateTableIfNotExists = create;
    },
    configureBlobs: x =>
    {
        x.CreateContainerIfNotExists = create;
        x.Serializer = new HybridSerializer();
        x.EnableCompilationAtRuntime();
    });
ServiceProvider provider = services.BuildServiceProvider();

MyTableContext context = provider.GetRequiredService<MyTableContext>();

await context.Models1.Where(x => true).BatchDeleteAsync();
await context.Models2.Where(x => true).BatchDeleteAsync();
await context.Models3.Where(x => true).BatchDeleteAsync();
await context.Models4.Where(x => true).BatchDeleteAsync();
await context.Models5.Where(x => true).BatchDeleteAsync();

await context.Models1.UpsertEntityAsync(new()
{
    PrettyName = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
});

await context.Models1.UpsertEntityAsync(new()
{
    PrettyName = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 5,
    MyProperty2 = "hallo 5"
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1",
    MyProperty3 = DateTimeOffset.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTimeOffset.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 5,
    MyProperty2 = "hallo 5",
    MyProperty3 = DateTimeOffset.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTimeOffset.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    PrettyRow = "this is a test",
    MyProperty1 = 15,
    MyProperty2 = "hallo 5",
    MyProperty3 = DateTimeOffset.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTimeOffset.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

List<Model2> models2 = await context.Models2.ToListAsync(); //should just return all my big models
Debug.Assert(models2.Count > 0);

List<Model2> enumFilters = await context.Models2.Where(x => x.MyProperty7 == ModelEnum.Yes && x.MyProperty8 == ModelEnum.No).ToListAsync(); //enum filtering should work
Debug.Assert(enumFilters.Count > 0);

List<Model> proxiedList = await context.Models1.SelectFields(x => x.PrettyName == "root" && x.PrettyRow != "").ToListAsync();
Debug.Assert(proxiedList?.Count > 0 && proxiedList.All(x => x.PrettyName == "root" && x.PrettyRow != ""));

int proxyWorksCount = await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow != "").CountAsync();
Debug.Assert(proxyWorksCount == proxiedList.Count);

#if !PublishAot
var proxySelectionWorks = await context.Models1.Select(x => new { x.PrettyName, x.PrettyRow }).ToListAsync();
#endif

List<Model> list1 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Take(3).ToListAsync();
Debug.Assert(list1.Count <= 3 && list1.All(x => x.PrettyName != null && x.PrettyRow != null && x.MyProperty1 != 0 && x.MyProperty2 != null)); // Should not contain more than 3 items with all properties filled in

List<Model> list2 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Take(3).Distinct(FuncComparer.Create((Model x) => x.MyProperty1)).ToListAsync();
Debug.Assert(list2.Count == 1 && list2.All(x => x.PrettyName != null && x.PrettyRow != null && x.MyProperty1 != 0 && x.MyProperty2 != null)); // Should contain 1 item with all properties filled in

List<Model> list3 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Distinct(FuncComparer.Create((Model x) => x.MyProperty2, StringComparer.OrdinalIgnoreCase)).Take(3).ToListAsync();
Debug.Assert(list3.Count == 1 && list3.All(x => x.PrettyName != null && x.PrettyRow != null && x.MyProperty1 != 0 && x.MyProperty2 != null)); // Should contain 1 item with all properties filled in

Model? first1 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync();
Debug.Assert(first1 != null && first1.PrettyName == null && first1.PrettyRow == null && first1.MyProperty1 != 0 && first1.MyProperty2 != null); // Should only fill in the selected properties

Model? first2 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => x.MyProperty1).FirstOrDefaultAsync();
Debug.Assert(first2 != null && first2.PrettyName == null && first2.PrettyRow == null && first2.MyProperty1 != 0 && first2.MyProperty2 == null); // Should only fill in MyProperty1

Model? first3 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync();
Debug.Assert(first3 != null && first3.PrettyName == null && first3.PrettyRow == null && first3.MyProperty1 != 0 && first3.MyProperty2 != null); // Should only fill in MyProperty1 and MyProperty2

#if !PublishAot
var firstTransformed1 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync();
Debug.Assert(firstTransformed1 != null && firstTransformed1.MyProperty1 != 0 && firstTransformed1.MyProperty2 != null); // Should return an anon type with only these two props

int firstTransformed2 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1).FirstOrDefaultAsync();
Debug.Assert(firstTransformed2 != 0); // Should return an int

TestTransformAndSelect? firstTransformed3 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync();
Debug.Assert(firstTransformed3 != null && firstTransformed3.prop1 != 0 && firstTransformed3.prop2 != null); // Should return a record with only these two props

TestTransformAndSelect? firstTransformed4 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelect(x.MyProperty1 + 1, x.MyProperty2 + "_test")).FirstOrDefaultAsync();
Debug.Assert(firstTransformed4 != null && firstTransformed4.prop1 != 0 && firstTransformed4.prop2 != null); // Should return a record with only these two props and included transformations

string? firstTransformed5 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1 + 1 + x.MyProperty2 + "_test").FirstOrDefaultAsync();
Debug.Assert(!string.IsNullOrEmpty(firstTransformed5)); // Should return a concatted string

TestTransformAndSelect? firstTransformed6 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => TestTransformAndSelect.Map(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync();
Debug.Assert(firstTransformed6 != null && firstTransformed6.prop1 != 0 && firstTransformed6.prop2 != null); // Should return a record with only these two props and included transformations

TestTransformAndSelect? firstTransformed7 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => x.Map()).FirstOrDefaultAsync();
Debug.Assert(firstTransformed7 != null && firstTransformed7.prop1 != 0 && firstTransformed7.prop2 != null); // Should at least work but gets everything

TestTransformAndSelectWithGuid? firstTransformed8 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, x.MyProperty2, Guid.Parse(x.PrettyRow))).FirstOrDefaultAsync();
Debug.Assert(firstTransformed8 != null && firstTransformed8.prop1 != 0 && firstTransformed8.prop2 != null); // Should only get 3 props and transform

TestTransformAndSelectWithGuid? firstTransformed9 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, "test", Guid.NewGuid())).FirstOrDefaultAsync();
Debug.Assert(firstTransformed9 != null && firstTransformed9.prop1 != 0 && firstTransformed9.prop2 != null); // Should only get one prop and transform

NestedTestTransformAndSelect? firstTransformed10 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new NestedTestTransformAndSelect(Guid.Parse(x.PrettyRow), new(x.MyProperty1 + (1 * 4), x.MyProperty2 + "_test"))).FirstOrDefaultAsync();
Debug.Assert(firstTransformed10 != null && firstTransformed10.id != Guid.Empty && firstTransformed10.test != null && firstTransformed10.test.prop1 != 0 && firstTransformed10.test.prop2 != null); // Should only get 3 props and transform into a nested object

StringFormatted? firstTransformed11 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new StringFormatted($"{x.PrettyRow} - {x.MyProperty1 + (1 * 4)}, {x.MyProperty2}_test")).FirstOrDefaultAsync();
Debug.Assert(firstTransformed11?.value != null); // Should only get 3 props and transform into a string

List<StringFormatted2> firstTransformed12 = await context.Models1.Select(x => new StringFormatted2($"{x.PrettyRow} - {x.MyProperty1 + (1 * 4)}, {x.MyProperty2}_test", null, x.Timestamp.GetValueOrDefault())).ToListAsync();
Debug.Assert(firstTransformed12?.All(x => x.Value != null) == true); // Should only get 4 props and transform into a string

List<StringFormatted2> firstTransformed13 = await context.Models1.Select(x => new StringFormatted2(string.Format("{0} - {1}, {2}_test {3}", new object[] { x.PrettyRow, x.MyProperty1 + (1 * 4), x.MyProperty2, x.Timestamp.GetValueOrDefault() }), null, x.Timestamp.GetValueOrDefault())).ToListAsync();
Debug.Assert(firstTransformed13?.All(x => x.Value != null && x.OtherValue == null && x.TimeStamp != default) == true); // Should only get 4 props and transform into a string
#endif

TableSet<Model> unknown = context.GetTableSet<Model>("randomname");
Debug.Assert(unknown != null); // Gives a tableset that wasn't defined on the original DbContext

List<Model> exists = await context.Models1.ExistsIn(x => x.MyProperty1, [1, 2, 3, 4]).Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 < 3).ToListAsync();
Debug.Assert(exists?.Count > 0); // Should return a list of existing models

try
{
    Model2 single = await context.Models2.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => x.MyProperty2).SingleAsync();
    Debug.Fail("Should throw");
}
catch (InvalidOperationException)
{
}

int fiveCount = await context.Models2.Where(x => x.MyProperty1 == 5).CountAsync();
int deleteCount = await context.Models2.Where(x => x.MyProperty1 == 5).BatchDeleteTransactionAsync();
Debug.Assert(deleteCount == fiveCount);

List<Model2> newModels2 = await context.Models2.ToListAsync();
Debug.Assert(newModels2.Count == (models2.Count - deleteCount));

int updateCount = await context.Models2.Where(x => x.MyProperty1 == 1).BatchUpdateTransactionAsync(x => new() { MyProperty2 = "hallo 1 updated" });
List<Model2> updatedModels = await context.Models2.Where(x => x.MyProperty2 == "hallo 1 updated").ToListAsync();
Debug.Assert(updateCount == updatedModels.Count);

var prettyItem = new { PrettyRow = "this is a test" };
List<Model2> visitorWorks = await context.Models2.Where(x => x.PrettyRow == prettyItem.PrettyRow).ToListAsync();
Debug.Assert(visitorWorks.Count > 0);
Debug.Assert(visitorWorks[0].PrettyRow == prettyItem.PrettyRow);
visitorWorks = await context.Models2.Where(x => x.PrettyRow != prettyItem.PrettyRow).ToListAsync();
Debug.Assert(visitorWorks.Count > 0);
Debug.Assert(visitorWorks[0].PrettyRow != prettyItem.PrettyRow);

Model mergeTest = new()
{
    PrettyName = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
};
await context.Models1.UpsertEntityAsync(mergeTest);
await context.Models1.UpdateAsync(() => new()
{
    PrettyName = "root",
    PrettyRow = mergeTest.PrettyRow,
    MyProperty1 = 5
});
Debug.Assert((await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).AsAsyncEnumerable().Select(x => x.MyProperty1).FirstAsync()) == 5);

#if !PublishAot
int mergeCount = await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).BatchUpdateAsync(x => new()
{
    MyProperty1 = x.MyProperty1 + 1
});
Debug.Assert(mergeCount == 1);
Debug.Assert((await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).AsAsyncEnumerable().Select(x => x.MyProperty1).FirstAsync()) == 6);
mergeCount = await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).BatchUpdateTransactionAsync(x => new()
{
    MyProperty1 = x.MyProperty1 - 1,
    MyProperty2 = Randoms.String(),
    MyProperty9 = Randoms.From(x.MyProperty3.ToString(), Randoms.String())

});
Debug.Assert(mergeCount == 1);
Debug.Assert((await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).AsAsyncEnumerable().Select(x => x.MyProperty1).FirstAsync()) == 5);
#endif

await context.Models1.UpsertAsync(() => new()
{
    PrettyName = "root",
    PrettyRow = mergeTest.PrettyRow,
    MyProperty1 = 5,
    MyProperty6 = ModelEnum.No
});

#if !PublishAot
await context.Models4Blob.DeleteAllEntitiesAsync("root");

string blobId1 = Guid.NewGuid().ToString("N");
await context.Models4Blob.AddEntityAsync(new()
{
    PrettyPartition = "root",
    PrettyRow = blobId1,
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
});

string blobId2 = Guid.NewGuid().ToString("N");
await context.Models4Blob.AddEntityAsync(new()
{
    PrettyPartition = "root",
    PrettyRow = blobId2,
    MyProperty1 = 2,
    MyProperty2 = "hallo 2"
});

Model4? blob1 = await context.Models4Blob.GetEntityOrDefaultAsync("root", blobId1);
Debug.Assert(blob1 != null);
Debug.Assert(blob1.MyProperty1 == 1 && blob1.MyProperty2 == "hallo 1");

Model4? blob2 = await context.Models4Blob.GetEntityOrDefaultAsync("root", blobId2);
Debug.Assert(blob2 != null);
Debug.Assert(blob2.MyProperty1 == 2 && blob2.MyProperty2 == "hallo 2");

Model4? blob3 = await context.Models4Blob.GetEntityOrDefaultAsync("root", Guid.NewGuid().ToString("N"));
Debug.Assert(blob3 == null);

List<Model4> blobResult1 = await context.Models4Blob.Where(x => x.PrettyPartition == "root")
                                       .Where(x => x.PrettyRow == blobId2)
                                       .Where(x => x.MyProperty1 == 2)
                                       .Where(x => x.MyProperty2 == "hallo 2")
                                       .ToListAsync(); // Iterate by Tags and Complex Filter
Debug.Assert(blobResult1.Count == 1);
Debug.Assert(blobResult1[0].MyProperty1 == 2 && blobResult1[0].MyProperty2 == "hallo 2");

blobResult1 = await context.Models4Blob.Where(x => x.PrettyPartition == "root")
                                       .Where(x => x.PrettyRow == blobId2)
                                       .Where(x => x.MyProperty1 == 2)
                                       .ToListAsync(); // Iterate By Tags
Debug.Assert(blobResult1.Count == 1);
Debug.Assert(blobResult1[0].MyProperty1 == 2 && blobResult1[0].MyProperty2 == "hallo 2");

blobResult1 = await context.Models4Blob.Where(x => x.PrettyPartition == "root" && x.PrettyRow == blobId2).ToListAsync();
Debug.Assert(blobResult1.Count == 1);
Debug.Assert(blobResult1[0].MyProperty1 == 2 && blobResult1[0].MyProperty2 == "hallo 2");
#endif

Model5 appendModel5 = new()
{
    Id = "root",
    ContinuationToken = "test",
    Entries =
    [
        new Model5Entry
        {
            Creation = DateTimeOffset.UtcNow,
            Duration = Random.Shared.Next(500, 2000)
        }
    ]
};

async Task AppendingTest()
{
    //if (await context.Models5Blob.ExistsAsync("root", "test"))
    try
    {
        using Stream stream = BinaryData.FromString($"|{DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeSeconds()};{Random.Shared.Next(500, 2000)}").ToStream();
        await context.Models5Blob.AppendAsync("root", "test", stream);
    }
    //else
    catch (Azure.RequestFailedException ex) when (ex.ErrorCode is "BlobNotFound")
    {
        await context.Models5Blob.UpsertEntityAsync(appendModel5);
    }
}

await context.Models5Blob.DeleteAllEntitiesAsync("root");

await AppendingTest(); // Create
Model5? appendedBlob = await context.Models5Blob.Where(x => x.Id == "root" && x.ContinuationToken == "test").FirstAsync();
Debug.Assert(appendedBlob is not null);
Debug.Assert(appendedBlob.Entries is not null);
Debug.Assert(appendedBlob.Entries.Length is 1);
Debug.Assert(appendedBlob.Entries[0].Creation.ToUnixTimeSeconds() == appendModel5.Entries[0].Creation.ToUnixTimeSeconds());
Debug.Assert(appendedBlob.Entries[0].Duration == appendModel5.Entries[0].Duration);

await AppendingTest(); // Append
Model5? appendedBlob2 = await context.Models5Blob.Where(x => x.Id == "root" && x.ContinuationToken == "test").FirstAsync();
Debug.Assert(appendedBlob2 is not null);
Debug.Assert(appendedBlob2.Entries is not null);
Debug.Assert(appendedBlob2.Entries.Length is 2);
Debug.Assert(appendedBlob2.Entries[0].Creation.ToUnixTimeSeconds() == appendedBlob.Entries[0].Creation.ToUnixTimeSeconds());
Debug.Assert(appendedBlob2.Entries[0].Duration == appendedBlob.Entries[0].Duration);
Debug.Assert(appendedBlob2.Entries[1].Creation.ToUnixTimeSeconds() > appendedBlob.Entries[0].Creation.ToUnixTimeSeconds());
Debug.Assert(appendedBlob2.Entries[1].Duration.HasValue);

await context.Models5BlobInJson.DeleteAllEntitiesAsync("root");
await context.Models5BlobInJson.AddEntityAsync(appendModel5);

var jsonBlob = await context.Models5BlobInJson.Where(x => x.Id == "root" && x.ContinuationToken == "test").FirstOrDefaultAsync();
Debug.Assert(jsonBlob is not null);
Debug.Assert(jsonBlob.Id is "root");
Debug.Assert(jsonBlob.ContinuationToken is "test");
Debug.Assert(jsonBlob.Entries is not null);
Debug.Assert(jsonBlob.Entries.Length is 1);
Debug.Assert(jsonBlob.Entries[0].Creation == appendModel5.Entries[0].Creation);
Debug.Assert(jsonBlob.Entries[0].Duration == appendModel5.Entries[0].Duration);

await context.Models4Blob.DeleteAllEntitiesAsync("root");

await context.Models4Blob.AddEntityAsync(new()
{
    PrettyPartition = "root",
    PrettyRow = "pretty1",
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
});

await context.Models4Blob.AddEntityAsync(new()
{
    PrettyPartition = "root",
    PrettyRow = "pretty2",
    MyProperty1 = 2,
    MyProperty2 = "hallo 2"
});

await context.Models4Blob.AddEntityAsync(new()
{
    PrettyPartition = "root",
    PrettyRow = "pretty3",
    MyProperty1 = 3,
    MyProperty2 = "hallo 3"
});

#if !PublishAot
var blobSearch = await context.Models4Blob.Where(x => x.PrettyPartition == "root").ExistsIn(x => x.PrettyRow, ["pretty1", "pretty2", "pretty4"]).ToListAsync();

Debug.Assert(blobSearch is not null);
Debug.Assert(blobSearch.Count is 2);
Debug.Assert(blobSearch.All(x => x.PrettyPartition == "root"));
Debug.Assert(blobSearch.Any(x => x.PrettyRow == "pretty1"));
Debug.Assert(blobSearch.Any(x => x.PrettyRow == "pretty2"));

blobSearch = await context.Models4Blob.ExistsIn(x => x.PrettyPartition, ["root", "root2"])
                                      .ExistsIn(x => x.PrettyRow, ["pretty1", "pretty2", "pretty4"])
                                      .NotExistsIn(x => x.PrettyRow, ["pretty3"])
                                      .ToListAsync();

Debug.Assert(blobSearch is not null);
Debug.Assert(blobSearch.Count is 2);
Debug.Assert(blobSearch.All(x => x.PrettyPartition == "root"));
Debug.Assert(blobSearch.Any(x => x.PrettyRow == "pretty1"));
Debug.Assert(blobSearch.Any(x => x.PrettyRow == "pretty2"));

blobSearch = await context.Models4Blob.ExistsIn(x => x.PrettyPartition, ["root", "root2"])
                                      .ExistsIn(x => x.PrettyRow, ["pretty1", "pretty2", "pretty4"])
                                      .NotExistsIn(x => x.PrettyRow, ["pretty3"])
                                      .Where(x => x.MyProperty1 == 2)
                                      .ToListAsync();

Debug.Assert(blobSearch is not null);
Debug.Assert(blobSearch.Count is 1);
Debug.Assert(blobSearch.All(x => x.PrettyPartition == "root"));
Debug.Assert(blobSearch.All(x => x.PrettyRow == "pretty2"));
Debug.Assert(blobSearch.All(x => x.MyProperty1 == 2));
#endif

#nullable disable
namespace TableStorage.Tests.Models
{
    public static class Randoms
    {
        public static string String() => Guid.NewGuid().ToString("N");
        public static string From(string value, string value2) => value + String() + value2;
    }

    public abstract class BaseModel
    {
        public string CommonProperty { get; set; }
        public virtual int BaseId { get; set; }
        public virtual int BaseId2 { get; set; }
    }

    [TableSet(TrackChanges = true, PartitionKey = "PrettyName", RowKey = "PrettyRow", SupportBlobs = true)]
    [TableSetProperty(typeof(int), "MyProperty1", Tag = true)]
    [TableSetProperty(typeof(string), "MyProperty2")]
    [TableSetProperty(typeof(ModelEnum), "MyProperty3")]
    [TableSetProperty(typeof(ModelEnum?), "MyProperty4")]
    [TableSetProperty(typeof(Nullable<ModelEnum>), "MyProperty6")]
    [TableSetProperty(typeof(HttpStatusCode), "MyProperty7")]
    [TableSetProperty(typeof(HttpStatusCode?), "MyProperty8")]
    [TableSetProperty(typeof(string), "MyProperty9")]
    public partial class Model : BaseModel
    {
        // Define as partial if you want to have changetracking
        [Tag]
        public partial ModelEnum? MyProperty5 { get; set; }
    }

    [TableSet(RowKey = "PrettyRow", SupportBlobs = true)]
    public partial class Model2 : BaseModel
    {
        public static string RandomHelpingString { get; } = "Test"; // Should not be generated as a property

        public partial int MyProperty1 { get; set; }
        public string MyProperty2 { get; set; }
        public System.DateTimeOffset? MyProperty3 { get; set; }
        public System.Guid? MyProperty4 { get; set; }
        public System.DateTimeOffset MyProperty5 { get; set; }
        public System.Guid MyProperty6 { get; set; }
        public ModelEnum MyProperty7 { get; set; }
        public ModelEnum? MyProperty8 { get; set; }
        public Nullable<ModelEnum> MyProperty9 { get; set; }

        public override int BaseId2 { get; set; } = 10; // Override a base property to test if it works
    }

    public enum ModelEnum
    {
        Yes,
        No
    }

    [TableSet]
    public partial class Model3
    {
    }

#nullable enable
    [TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow), SupportBlobs = true, TrackChanges = true)]
    [ProtoContract(IgnoreListHandling = true)] // Important to ignore list handling because we are generating an IDictionary implementation that is not supported by protobuf
    public partial class Model4
    {
        [ProtoMember(1)] public partial string PrettyPartition { get; set; } // We can partial the PK and RowKey to enable custom serialization attributes
        [ProtoMember(2)] public partial string PrettyRow { get; set; }
        [ProtoMember(3)] public partial int MyProperty1 { get; set; }
        [ProtoMember(4)] public partial string MyProperty2 { get; set; }
        [ProtoMember(5)] public partial string? MyNullableProperty2 { get; set; }
    }
#nullable disable

#if TABLESTORAGE_BLOBS
    public class TestBlobs
    {

    }
#endif

    [TableSet(PartitionKey = "Id", RowKey = "ContinuationToken", SupportBlobs = true, DisableTables = true)]
    public partial class Model5
    {
        public partial string Id { get; set; }
        public partial string ContinuationToken { get; set; }
        public partial Model5Entry[] Entries { get; set; }
    }

    public sealed class Model5Entry
    {
        public DateTimeOffset Creation { get; set; }
        public long? Duration { get; set; }
    }

    [TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
    public partial class TestModel
    {
        public partial string PrettyPartition { get; set; }
        public partial string PrettyRow { get; set; }
        public partial int MyProperty1 { get; set; }
        public partial string MyProperty2 { get; set; }
        public partial string MyNullableProperty2 { get; set; }
    }
}

namespace TableStorage.Tests.Contexts
{
    using TableStorage.Tests.Models;

    [TableContext]
    public partial class MyTableContext
    {
        public TableSet<Model> Models1 { get; set; }
        public TableSet<Model2> Models2 { get; private set; }
        public TableSet<Model> Models3 { get; }
        public TableSet<Model> Models4 { get; init; }
        public TableSet<Model3> Models5 { get; init; }

        public BlobSet<Model> Models1Blob { get; set; }
        public BlobSet<Model4> Models4Blob { get; set; }
        public BlobSet<Model2> Models2Blob { get; }
        public AppendBlobSet<Model5> Models5Blob { get; }
        public AppendBlobSet<Model5> Models5BlobInJson { get; }
    }

    [JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web,
        UseStringEnumConverter = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(Model5))]
    public partial class ModelSerializationContext : JsonSerializerContext;
}

public record TestTransformAndSelectWithGuid(int prop1, string prop2, Guid id);

public record NestedTestTransformAndSelect(Guid id, TestTransformAndSelect test);
public record StringFormatted(string value);
public record StringFormatted2(string Value, string OtherValue, DateTimeOffset TimeStamp);

public record TestTransformAndSelect(int prop1, string prop2)
{
    public static TestTransformAndSelect Map(int prop1, string prop2)
    {
        return new(prop1, prop2);
    }
}

public static class Mapper
{
    public static TestTransformAndSelect Map(this TableStorage.Tests.Models.Model model)
    {
        return new(model.MyProperty1, model.MyProperty2);
    }
}

public sealed class HybridSerializer : IBlobSerializer
{
    public async ValueTask<T> DeserializeAsync<T>(string table, Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
    {
#if !PublishAot
        if (table is "models4blob")
        {
            return Serializer.Deserialize<T>(entity);
        }
#endif

        if (table is "models5blob")
        {
            using StreamReader reader = new(entity);
            string simple = await reader.ReadToEndAsync(cancellationToken);
            string[] parts = simple.Split('\\', 3);

            return (T)(object)new Model5
            {
                Id = parts[0],
                ContinuationToken = parts[1],
                Entries = [.. parts[2].Split('|').Select(x =>
                {
                    string[] entryParts = x.Split(';');
                    return new Model5Entry
                    {
                        Creation = DateTimeOffset.FromUnixTimeSeconds(long.Parse(entryParts[0])),
                        Duration = entryParts[1] switch
                        {
                            null or "" => null,
                            _ => long.Parse(entryParts[1])
                        }
                    };
                })]
            };
        }

        if (table is "models5blobinjson")
        {
            return (T)(object)await JsonSerializer.DeserializeAsync(entity, ModelSerializationContext.Default.Model5, cancellationToken);
        }

        BinaryData data = await BinaryData.FromStreamAsync(entity, cancellationToken);
        return data.ToObjectFromJson<T>(ModelSerializationContext.Default.Options);
    }

    public BinaryData Serialize<T>(string table, T entity) where T : IBlobEntity
    {
        if (table is "models4blob" && entity is Model4 model4)
        {
            using MemoryStream stream = new();
            Serializer.Serialize(stream, model4);
            return new(stream.ToArray());
        }

        if (table is "models5blob" && entity is Model5 model5)
        {
            string simple = $"{model5.Id}\\{model5.ContinuationToken}\\{string.Join("|", model5.Entries.Select(x => $"{x.Creation.ToUnixTimeSeconds()};{x.Duration}"))}";
            return BinaryData.FromString(simple);
        }

        if (table is "models5blobinjson" && entity is Model5 model5InJson)
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(model5InJson, ModelSerializationContext.Default.Model5);
            return BinaryData.FromBytes(data);
        }

        return BinaryData.FromObjectAsJson(entity, ModelSerializationContext.Default.Options);
    }
}