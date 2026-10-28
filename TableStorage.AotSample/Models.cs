using System.Text.Json.Serialization;
using TableStorage.Fluent;

namespace TableStorage.AotSample;

[TableContext]
public partial class SampleContext
{
    public TableSet<Product> Products { get; set; }
    public TableSet<FluentTableEntity<Order, Invoice>> Documents { get; set; }
    public BlobSet<Note> Notes { get; set; }
}

[TableSet(PartitionKey = nameof(Folder), RowKey = nameof(Name), SupportBlobs = true, DisableTables = true)]
public partial class Note
{
    public partial string Folder { get; set; }
    public partial string Name { get; set; }
    public partial string Text { get; set; }
    public partial int Version { get; set; }
}

[JsonSerializable(typeof(Note))]
public partial class SampleJsonContext : JsonSerializerContext;

[TableSet(PartitionKey = nameof(Category), RowKey = nameof(Id))]
public partial class Product
{
    public partial string Category { get; set; }
    public partial string Id { get; set; }
    public partial string Name { get; set; }
    public partial int Quantity { get; set; }
}

[TableSet(PartitionKey = nameof(Customer), RowKey = nameof(OrderId))]
public partial class Order
{
    public partial string Customer { get; set; }
    public partial string OrderId { get; set; }
    public partial int Amount { get; set; }
}

[TableSet(PartitionKey = nameof(Customer), RowKey = nameof(InvoiceId))]
public partial class Invoice
{
    public partial string Customer { get; set; }
    public partial string InvoiceId { get; set; }
    public partial bool Paid { get; set; }
}
