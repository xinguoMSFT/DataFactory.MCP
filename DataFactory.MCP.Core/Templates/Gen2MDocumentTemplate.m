[StagingDefinition = [Kind = "FastCopy"]]
section Section1;

// Source Query with DataDestinations attribute pointing to the destination query
[DataDestinations = {[Definition = [Kind = "Reference", QueryName = "{tableName}_DataDestination", IsNewTarget = true], Settings = [Kind = "Automatic", TypeSettings = [Kind = "Table"]]]}]
shared {tableName} = let
  Source = ...,  // Your data source connector
  // Add any transformations here
  Result = ...
in
  Result;

// DataDestination Query - navigates to target Lakehouse table
shared {tableName}_DataDestination = let
  Pattern = Lakehouse.Contents([HierarchicalNavigation = null, CreateNavigationProperties = false, EnableFolding = false]),
  Navigation_1 = Pattern{[workspaceId = "{workspaceId}"]}[Data],
  Navigation_2 = Navigation_1{[lakehouseId = "{lakehouseId}"]}[Data],
  TableNavigation = Navigation_2{[Name = "{tableName}"]}?[Data]?
in
  TableNavigation;
