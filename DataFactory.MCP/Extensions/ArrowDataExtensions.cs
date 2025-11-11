using DataFactory.MCP.Models.Dataflow;

namespace DataFactory.MCP.Extensions;

/// <summary>
/// Extensions for formatting Apache Arrow data responses
/// </summary>
public static class ArrowDataExtensions
{
    /// <summary>
    /// Formats Apache Arrow data summary for display with full data
    /// </summary>
    /// <param name="summary">The query result summary</param>
    public static object ToFormattedArrowSummary(this QueryResultSummary summary)
    {
        var formatted = new
        {
            DataFormat = "Apache Arrow Stream",
            ParsingStatus = summary.ArrowParsingSuccess ? "Success" : "Fallback to text parsing",
            ParsingError = summary.ArrowParsingError,

            Schema = summary.ArrowSchema != null ? new
            {
                FieldCount = summary.ArrowSchema.FieldCount,
                Columns = summary.ArrowSchema.Columns?.Select(c => new
                {
                    Name = c.Name,
                    DataType = c.DataType,
                    IsNullable = c.IsNullable,
                    HasMetadata = c.Metadata?.Any() == true
                })
            } : null,

            DataSummary = new
            {
                TotalRows = summary.EstimatedRowCount,
                BatchCount = summary.BatchCount,
                Columns = summary.Columns,
                FullDataAvailable = summary.SampleData?.Keys.ToList()
            },

            Format = summary.Format,

            ProcessingNotes = new[]
            {
                summary.ArrowParsingSuccess
                    ? "Data successfully parsed using Apache Arrow libraries"
                    : "Arrow parsing failed, used text-based extraction as fallback",

                $"Extracted {summary.Columns?.Count ?? 0} columns with {summary.EstimatedRowCount ?? 0} estimated rows",

                summary.BatchCount > 0
                    ? $"Data organized in {summary.BatchCount} Arrow record batches"
                    : "Batch information not available"
            }
        };

        return formatted;
    }

    private static object FormatAsTable(Dictionary<string, List<object>> structuredData)
    {
        if (!structuredData.Any())
            return CreateEmptyTable();

        // Get all column names (exclude PQ Arrow Metadata)
        var columns = structuredData.Keys.Where(k => k != "PQ Arrow Metadata").ToList();

        // Get the number of rows (assume all columns have the same number of rows)
        var rowCount = structuredData.Values.FirstOrDefault()?.Count ?? 0;

        // Create column definitions
        var columnDefinitions = columns.Select(col => new
        {
            Name = col,
            DataType = InferDataType(structuredData.ContainsKey(col) ? structuredData[col] : new List<object>())
        }).ToList();

        // Create table rows
        var rows = new List<Dictionary<string, object>>();
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                if (structuredData.ContainsKey(col) && rowIndex < structuredData[col].Count)
                {
                    row[col] = structuredData[col][rowIndex]?.ToString() ?? "";
                }
                else
                {
                    row[col] = "";
                }
            }
            rows.Add(row);
        }

        return new
        {
            Format = "Table",
            RowCount = rowCount,
            ColumnCount = columns.Count,
            Summary = $"{rowCount} rows × {columns.Count} columns",
            Columns = columnDefinitions,
            Rows = rows
        };
    }

    private static object CreateEmptyTable()
    {
        return new
        {
            Format = "Table",
            RowCount = 0,
            ColumnCount = 0,
            Columns = Array.Empty<object>(),
            Rows = Array.Empty<object>(),
            Summary = "No data available"
        };
    }

    private static string InferDataType(List<object> values)
    {
        if (!values.Any()) return "Unknown";

        var firstNonNull = values.FirstOrDefault(v => v != null);
        return firstNonNull?.GetType().Name ?? "Null";
    }

    /// <summary>
    /// Creates a comprehensive Arrow data report with structured data
    /// </summary>
    /// <param name="response">The execution response</param>
    public static object CreateArrowDataReport(this ExecuteDataflowQueryResponse response)
    {
        var arrowSummary = response.Summary?.ToFormattedArrowSummary();

        // Extract structured table data directly from the summary
        var tableData = response.Summary?.StructuredSampleData != null
            ? FormatAsTable(response.Summary.StructuredSampleData)
            : CreateEmptyTable();

        return new
        {
            // Put structured table data first for MCP client rendering
            Table = tableData,

            ExecutionSummary = new
            {
                Success = response.Success,
                ContentType = response.ContentType,
                ContentLength = response.ContentLength,
                DataSize = FormatBytes(response.ContentLength),
                ExecutionMetadata = response.Metadata
            },

            ArrowData = arrowSummary
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}