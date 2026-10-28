using Microsoft.Extensions.DependencyInjection;
using TableStorage;
using TableStorage.AotSample;
using TableStorage.Fluent;
using TableStorage.Linq;

string connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("TABLESTORAGE_CONNECTIONSTRING")
    ?? "UseDevelopmentStorage=true";

ServiceCollection services = new();
services.AddSampleContext(connectionString, configureBlobs: x => x.Serializer = new AotJsonBlobSerializer(SampleJsonContext.Default));
using ServiceProvider provider = services.BuildServiceProvider();
SampleContext context = provider.GetRequiredService<SampleContext>();

string category = "aot-" + Guid.NewGuid().ToString("N");
int failures = 0;

await Run("Upsert entities", async () =>
{
    await context.Products.UpsertEntityAsync(new() { Category = category, Id = "1", Name = "Apple", Quantity = 5 });
    await context.Products.UpsertEntityAsync(new() { Category = category, Id = "2", Name = "Pear", Quantity = 15 });
    return true;
});

await Run("FindAsync", async () =>
{
    Product? product = await context.Products.FindAsync(category, "1");
    return product?.Name == "Apple";
});

await Run("Where with captured and static members", async () =>
{
    List<Product> products = await context.Products
        .Where(x => x.Category == category && x.Quantity > Limits.MinimumQuantity)
        .ToListAsync();

    return products is [{ Name: "Pear" }];
});

await Run("ExistsIn", async () =>
{
    List<Product> products = await context.Products
        .Where(x => x.Category == category)
        .ExistsIn(x => x.Id, ["1", "2"])
        .ToListAsync();

    return products.Count is 2;
});

await Run("SelectFields", async () =>
{
    List<Product> products = await context.Products
        .Where(x => x.Category == category)
        .SelectFields(x => x.Name)
        .ToListAsync();

    return products.Count is 2 && products.All(x => x.Name is not null && x.Quantity is 0);
});

await Run("Fluent upsert and $type filter", async () =>
{
    await context.Documents.UpsertEntityAsync(new Order { Customer = category, OrderId = "o1", Amount = 10 });
    await context.Documents.UpsertEntityAsync(new Invoice { Customer = category, InvoiceId = "i1", Paid = true });

    List<Order> orders = await context.Documents
        .WhereFirstType()
        .Where(x => x.Customer == category)
        .ToListAsync();

    List<Invoice> invoices = await context.Documents
        .WhereSecondType()
        .Where(x => x.Customer == category)
        .ToListAsync();

    return orders is [{ Amount: 10 }] && invoices is [{ Paid: true }];
});

await Run("Blob upsert and get", async () =>
{
    await context.Notes.UpsertEntityAsync(new() { Folder = category, Name = "readme", Text = "Hello AOT", Version = 1 });
    await context.Notes.UpsertEntityAsync(new() { Folder = category, Name = "todo", Text = "Ship it", Version = 2 });

    Note? note = await context.Notes.GetEntityOrDefaultAsync(category, "readme");
    return note is { Text: "Hello AOT", Version: 1 };
});

await Run("Blob query by partition", async () =>
{
    List<Note> notes = await context.Notes
        .Where(x => x.Folder == category)
        .ToListAsync();

    return notes.Count is 2;
});

await Run("Blob query by partition and row", async () =>
{
    List<Note> notes = await context.Notes
        .Where(x => x.Folder == category && x.Name == "todo")
        .ToListAsync();

    return notes is [{ Version: 2 }];
});

await Run("Delete entities", async () =>
{
    await context.Products.Where(x => x.Category == category).BatchDeleteAsync();
    await context.Documents.Where(x => x.PartitionKey == category).BatchDeleteAsync();
    await context.Notes.DeleteAllEntitiesAsync(category);
    return await context.Products.FindAsync(category, "1") is null
        && await context.Notes.GetEntityOrDefaultAsync(category, "readme") is null;
});

Console.WriteLine(failures is 0 ? "All checks passed" : $"{failures} check(s) failed");
return failures is 0 ? 0 : 1;

async Task Run(string name, Func<Task<bool>> check)
{
    try
    {
        bool success = await check();
        Console.WriteLine($"{(success ? "PASS" : "FAIL")} {name}");

        if (!success)
        {
            failures++;
        }
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(string.Join(Environment.NewLine, (ex.StackTrace ?? "").Split(Environment.NewLine).Take(6)));
    }
}

internal static class Limits
{
    public static readonly int MinimumQuantity = 10;
}
