# TableStorage.Fluent.Extended (deprecated)

This package is no longer needed. Starting with version 7.0, [TableStorage.Fluent](https://www.nuget.org/packages/TableStorage.Fluent) generates fluent entities for 2 to 16 types in your own project, using a source generator.

This final release is empty and only depends on `TableStorage.Fluent`, so existing projects keep working.

## Migrating

1. Remove the `TableStorage.Fluent.Extended` package reference.
2. Make sure you reference `TableStorage.Fluent` 7.0 or later.

Fluent entities used by a `[TableContext]` are generated automatically. To generate other arities, add:

```csharp
[assembly: TableStorage.Fluent.GenerateFluentEntities(5, 6)]
```

The `TABLESTORAGE_FLUENT_EXTENDED` compilation symbol is no longer defined.
