using System.Text.Json;
using System.Reflection;

namespace DataFactory.MCP.Services;

/// <summary>
/// Provides M Query templates and authoring guidance loaded from embedded resources.
/// Loads .m files for document templates and .json files for data sources and guidance.
/// </summary>
public class MTemplateProvider
{
    private readonly JsonDocument _dataSources;
    private readonly JsonDocument _guidance;
    private readonly string _gen2Template;
    private readonly string _gen1Template;

    public MTemplateProvider()
    {
        _dataSources = LoadJsonResource("Templates", "DataSourceTemplates.json");
        _guidance = LoadJsonResource("Guidance", "AuthoringGuidance.json");
        _gen2Template = LoadMTemplate("Templates", "Gen2MDocumentTemplate.m");
        _gen1Template = LoadMTemplate("Templates", "Gen1MDocumentTemplate.m");
    }

    #region Data Source Templates

    public DataSourceTemplate? GetDataSourceTemplate(string sourceType)
    {
        var dataSources = _dataSources.RootElement.GetProperty("dataSources");
        if (dataSources.TryGetProperty(sourceType.ToLowerInvariant(), out var source))
        {
            return new DataSourceTemplate
            {
                Name = source.GetProperty("name").GetString() ?? "",
                Description = source.GetProperty("description").GetString() ?? "",
                Template = source.GetProperty("template").GetString() ?? "",
                RequiredParameters = source.GetProperty("requiredParameters")
                    .EnumerateArray()
                    .Select(p => p.GetString() ?? "")
                    .ToArray()
            };
        }
        return null;
    }

    public IEnumerable<string> GetAvailableDataSources()
    {
        return _dataSources.RootElement.GetProperty("dataSources")
            .EnumerateObject()
            .Select(p => p.Name);
    }

    #endregion

    #region Gen2 Templates

    public string GetGen2SectionAttribute()
    {
        return _guidance.RootElement.GetProperty("gen2").GetProperty("sectionAttribute").GetString() ?? "";
    }

    public string GetGen2DocumentTemplate()
    {
        return _gen2Template;
    }

    public string GetGen2LakehouseDestinationTemplate()
    {
        // Extract the destination query from the Gen2 template
        var lines = _gen2Template.Split('\n');
        var startIndex = Array.FindIndex(lines, l => l.Contains("_DataDestination"));
        if (startIndex >= 0)
        {
            return string.Join("\n", lines.Skip(startIndex));
        }
        return "";
    }

    public string GetGen2DataDestinationsAttribute()
    {
        return _guidance.RootElement.GetProperty("gen2").GetProperty("dataDestinationsAttribute").GetString() ?? "";
    }

    #endregion

    #region Gen1 Templates

    public string GetGen1DocumentTemplate()
    {
        return _gen1Template;
    }

    public string GetGen1DefaultModelStorageQuery()
    {
        return _guidance.RootElement.GetProperty("gen1").GetProperty("defaultModelStorageQuery").GetString() ?? "";
    }

    public string GetGen1StagingAttribute()
    {
        return _guidance.RootElement.GetProperty("gen1").GetProperty("stagingAttribute").GetString() ?? "";
    }

    #endregion

    #region Guidance

    public IEnumerable<DataTypeInfo> GetDataTypes()
    {
        return _guidance.RootElement.GetProperty("dataTypes")
            .EnumerateArray()
            .Select(dt => new DataTypeInfo
            {
                Type = dt.GetProperty("type").GetString() ?? "",
                MType = dt.GetProperty("mType").GetString() ?? "",
                ForNullable = dt.GetProperty("forNullable").GetString() ?? ""
            });
    }

    public NamingRules GetNamingRules()
    {
        var rules = _guidance.RootElement.GetProperty("namingRules");
        return new NamingRules
        {
            SimpleNames = rules.GetProperty("simpleNames").GetString() ?? "",
            SpecialCharacters = rules.GetProperty("specialCharacters").GetString() ?? "",
            ColumnNamesWithSpaces = rules.GetProperty("columnNamesWithSpaces").GetString() ?? ""
        };
    }

    public string[] GetNextSteps()
    {
        return _guidance.RootElement.GetProperty("nextSteps")
            .EnumerateArray()
            .Select(s => s.GetString() ?? "")
            .ToArray();
    }

    public AuthoringStepInfo[] GetGen2Steps()
    {
        return GetSteps("gen2");
    }

    public AuthoringStepInfo[] GetGen1Steps()
    {
        return GetSteps("gen1");
    }

    public string[] GetGen2Tips()
    {
        return GetTips("gen2");
    }

    public string[] GetGen1Tips()
    {
        return GetTips("gen1");
    }

    public string GetGen2Pattern()
    {
        return _guidance.RootElement.GetProperty("gen2").GetProperty("pattern").GetString() ?? "";
    }

    public string GetGen1Pattern()
    {
        return _guidance.RootElement.GetProperty("gen1").GetProperty("pattern").GetString() ?? "";
    }

    public string GetGen2NamingConvention()
    {
        return _guidance.RootElement.GetProperty("gen2").GetProperty("namingConvention").GetString() ?? "";
    }

    public string GetGen1NamingConvention()
    {
        return _guidance.RootElement.GetProperty("gen1").GetProperty("namingConvention").GetString() ?? "";
    }

    #endregion

    #region Source Detection

    public string DetectSourceType(string requirement, string? explicitType = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitType))
            return explicitType.ToLowerInvariant();

        var lower = requirement.ToLowerInvariant();
        var keywords = _guidance.RootElement.GetProperty("sourceTypeKeywords");

        foreach (var sourceType in keywords.EnumerateObject())
        {
            foreach (var keyword in sourceType.Value.EnumerateArray())
            {
                if (lower.Contains(keyword.GetString() ?? ""))
                {
                    return sourceType.Name;
                }
            }
        }

        return "sql"; // Default
    }

    public bool ContainsTransformationKeywords(string text)
    {
        return ContainsKeywords(text, "transformationKeywords");
    }

    public bool ContainsFilteringKeywords(string text)
    {
        return ContainsKeywords(text, "filteringKeywords");
    }

    public bool ContainsAggregationKeywords(string text)
    {
        return ContainsKeywords(text, "aggregationKeywords");
    }

    #endregion

    #region Helpers

    private AuthoringStepInfo[] GetSteps(string version)
    {
        return _guidance.RootElement.GetProperty(version).GetProperty("steps")
            .EnumerateArray()
            .Select(s => new AuthoringStepInfo
            {
                Step = s.GetProperty("step").GetInt32(),
                Action = s.GetProperty("action").GetString() ?? "",
                Description = s.GetProperty("description").GetString() ?? ""
            })
            .ToArray();
    }

    private string[] GetTips(string version)
    {
        return _guidance.RootElement.GetProperty(version).GetProperty("tips")
            .EnumerateArray()
            .Select(t => t.GetString() ?? "")
            .ToArray();
    }

    private bool ContainsKeywords(string text, string keywordProperty)
    {
        var lower = text.ToLowerInvariant();
        return _guidance.RootElement.GetProperty(keywordProperty)
            .EnumerateArray()
            .Any(k => lower.Contains(k.GetString() ?? ""));
    }

    private static JsonDocument LoadJsonResource(string folder, string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName));

        if (resourceName == null)
        {
            // Fallback: try to load from file system (for development)
            var basePath = Path.GetDirectoryName(assembly.Location) ?? "";
            var filePath = Path.Combine(basePath, folder, fileName);

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonDocument.Parse(json);
            }

            throw new FileNotFoundException($"Resource not found: {folder}/{fileName}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonDocument.Parse(stream);
    }

    private static string LoadMTemplate(string folder, string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName));

        if (resourceName == null)
        {
            // Fallback: try to load from file system (for development)
            var basePath = Path.GetDirectoryName(assembly.Location) ?? "";
            var filePath = Path.Combine(basePath, folder, fileName);

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }

            throw new FileNotFoundException($"Template not found: {folder}/{fileName}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    #endregion
}

#region DTOs

public class DataSourceTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Template { get; set; } = "";
    public string[] RequiredParameters { get; set; } = Array.Empty<string>();
}

public class DataTypeInfo
{
    public string Type { get; set; } = "";
    public string MType { get; set; } = "";
    public string ForNullable { get; set; } = "";
}

public class NamingRules
{
    public string SimpleNames { get; set; } = "";
    public string SpecialCharacters { get; set; } = "";
    public string ColumnNamesWithSpaces { get; set; } = "";
}

public class AuthoringStepInfo
{
    public int Step { get; set; }
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
}

#endregion
