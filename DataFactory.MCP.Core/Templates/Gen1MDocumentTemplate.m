section Section1;

// 1. Source Query - loads data
shared SourceData = let
    Source = ...
    // Your data source connector
in
    Source;

// 2. Transform Query (optional) - applies transformations
shared TransformedData = let Source = SourceData,
// Add filters, column selections, type conversions here
Result = Source in Result;

// 3. Default Storage - required for writes
shared DefaultModelStorage = let DefaultModelStorage = Pipeline.DefaultModelStorage() in DefaultModelStorage;

// 4. Destination - defines target table
shared DataDestination =
    let
        Pattern = Lakehouse.Contents([]),
        Workspace = Pattern{[workspaceId = "{workspaceId}"]}[Data],
        Lakehouse = Workspace{[lakehouseId = "{lakehouseId}"]}[Data],
        Table = Lakehouse{[Id = "{tableName}", ItemKind = "Table"]} ?[Data]?
    in
        Table;

// 5. Column Mapping - maps columns
shared TransformForWrite =
    let
        Source = TransformedData,
        ColumnMappings = {{"SourceColumn1", "DestColumn1"}, {"SourceColumn2", "DestColumn2"}},
        MappedColumns = Table.TransformColumnNames(
            Source, each List.First(List.Select(ColumnMappings, (m) => m{0} = _)){1}
        )
    in
        MappedColumns;

// 6. Write Query - executes the load
[Staging = "DefaultModelStorage"]
shared WriteToDestination =
    let
        Result = Pipeline.ExecuteAction(
            "Microsoft.DataLake/WriteAction",
            [
                Source = TransformForWrite,
                Destination = DataDestination,
                WriteMode = "Append"
            ]
        )
    in
        Result;
