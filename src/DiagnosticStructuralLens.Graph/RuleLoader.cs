using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiagnosticStructuralLens.Graph;

public class RuleLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads rules from a JSON string.
    /// Overrides built-in rules if IDs match.
    /// </summary>
    public IReadOnlyList<ArchitectureRule> LoadRules(string jsonConfig)
    {
        var loadedRules = JsonSerializer.Deserialize<List<ArchitectureRule>>(jsonConfig, _options) 
                          ?? new List<ArchitectureRule>();
        
        // Merge with built-in rules
        var ruleMap = BuiltInRules.All.ToDictionary(r => r.Id, r => r);
        
        foreach (var rule in loadedRules)
        {
            // Override or Add
            ruleMap[rule.Id] = rule;
        }

        return ruleMap.Values.OrderBy(r => r.Id).ToList();
    }
}
