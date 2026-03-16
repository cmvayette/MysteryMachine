using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using DiagnosticStructuralLens.Core.Governance;

namespace DiagnosticStructuralLens.Core;

public interface IGovernanceEngine
{
    bool IsViolation(AtomLink link, CodeAtom source, CodeAtom target);
    List<string> GetViolationReasons(AtomLink link, CodeAtom source, CodeAtom target);
}

public class GovernanceEngine : IGovernanceEngine
{
    private readonly GovernanceConfig _config;
    private readonly Dictionary<string, Regex> _compiledPatterns = new();
    private readonly Dictionary<string, AtomSelector> _resolvedDefinitions = new();

    public GovernanceEngine(string configPath = "governance.yaml")
    {
        if (File.Exists(configPath))
        {
            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            
            // We deserialize to a helper first to handle polymorphism manually if needed, 
            // but YamlDotNet can handle it if we register types. 
            // For simplicity in this iteration, let's load specific lists directly or use a helper structure.
            // Actually, dynamic deserialization is tricky. Let's assume a simplified structure where we deserialize to a raw object graph or strict types.
            // A simpler approach for this phase: Load raw rules and inspect 'type' property.
            
            // To support polymorphism without complex setup, let's deserialize rules as Dictionary<string, object> and map manually.
            var rawConfig = deserializer.Deserialize<RawGovernanceConfig>(yaml);
            _config = MapRawConfig(rawConfig);
            
            CompilePatterns();
        }
        else
        {
            _config = new GovernanceConfig(); // Empty config
        }
    }

    // Helper for deserialization
    private class RawGovernanceConfig 
    {
        public string? Version { get; set; }
        public Dictionary<string, AtomSelector>? Definitions { get; set; }
        public List<Dictionary<string, object>>? Rules { get; set; }
    }

    private GovernanceConfig MapRawConfig(RawGovernanceConfig raw)
    {
#pragma warning disable CS8600, CS8601, CS8602, CS8604
        var config = new GovernanceConfig 
        { 
            Version = raw.Version ?? "1.0", 
            Definitions = raw.Definitions ?? new()
        };
        
        if (raw.Definitions != null)
        {
            foreach (var def in raw.Definitions)
            {
                if (def.Value != null)
                    _resolvedDefinitions[def.Key] = def.Value;
            }
        }

        if (raw.Rules != null)
        {
            foreach (var rawRule in raw.Rules)
            {
                if (!rawRule.TryGetValue("type", out var typeObj)) continue;
                string type = typeObj?.ToString() ?? "unknown";
                
                // Manual mapping
                GovernanceRule rule = null;
                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(rawRule);
                var deserializer = new DeserializerBuilder().Build();

                if (type == "forbidden") rule = deserializer.Deserialize<ForbiddenRule>(yaml)!;
                else if (type == "layering") rule = deserializer.Deserialize<LayeringRule>(yaml)!;
                else if (type == "visibility") rule = deserializer.Deserialize<VisibilityRule>(yaml)!;

                if (rule != null) config.Rules.Add(rule);
            }
        }
#pragma warning restore CS8600, CS8601, CS8602, CS8604
        return config;
    }

    private void CompilePatterns()
    {
        // Compile regexes for definitions
        if (_config.Definitions != null)
        {
            foreach (var def in _config.Definitions.Values)
            {
                if (!string.IsNullOrEmpty(def.Pattern) && !_compiledPatterns.ContainsKey(def.Pattern))
                {
                    _compiledPatterns[def.Pattern] = new Regex(def.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
            }
        }
        
        // Compile rules patterns (if inline)
        // ... (Simplified: Assuming mostly definitions used)
    }

    public bool IsViolation(AtomLink link, CodeAtom source, CodeAtom target)
    {
        if (source == null || target == null) return false;

        foreach (var rule in _config.Rules)
        {
            if (rule is ForbiddenRule forbidden)
            {
                if (forbidden.SourceRaw != null && forbidden.TargetRaw != null && 
                    Matches(source, forbidden.SourceRaw) && Matches(target, forbidden.TargetRaw))
                {
                    return true;
                }
            }
            else if (rule is LayeringRule layering)
            {
                // Find which layers source and target belong to
                int sourceLayer = GetLayerIndex(source, layering.LayersRaw);
                int targetLayer = GetLayerIndex(target, layering.LayersRaw);
                
                if (sourceLayer != -1 && targetLayer != -1)
                {
                    // Violations:
                    // 1. Calling upwards (Target < Source) -> Data calls Web
                    if (targetLayer < sourceLayer) return true;
                    
                    // 2. Strict violation: Calling more than 1 layer down (Target > Source + 1)
                    if (layering.Mode == "strict" && targetLayer > sourceLayer + 1) return true;
                }
            }
            else if (rule is VisibilityRule visibility)
            {
                if (visibility.TargetRaw != null && Matches(target, visibility.TargetRaw))
                {
                    bool isAllowed = false;
                    foreach (var consumer in visibility.AllowedConsumersRaw)
                    {
                        if (Matches(source, consumer))
                        {
                            isAllowed = true;
                            break;
                        }
                    }
                    if (!isAllowed) return true;
                }
            }
        }

        return false;
    }

    public List<string> GetViolationReasons(AtomLink link, CodeAtom source, CodeAtom target)
    {
        var reasons = new List<string>();
        
        foreach (var rule in _config.Rules)
        {
            if (rule is ForbiddenRule forbidden)
            {
                if (forbidden.SourceRaw != null && forbidden.TargetRaw != null && 
                    Matches(source, forbidden.SourceRaw) && Matches(target, forbidden.TargetRaw))
                {
                    reasons.Add(forbidden.Message ?? $"Forbidden dependency: {source.Name} -> {target.Name}");
                }
            }
            else if (rule is LayeringRule layering)
            {
                int sourceLayer = GetLayerIndex(source, layering.LayersRaw);
                int targetLayer = GetLayerIndex(target, layering.LayersRaw);

                if (sourceLayer != -1 && targetLayer != -1)
                {
                    if (targetLayer < sourceLayer)
                        reasons.Add(rule.Message ?? $"Layering Violation: {GetLayerName(sourceLayer, layering)} cannot depend on higher layer {GetLayerName(targetLayer, layering)}");
                    
                    if (layering.Mode == "strict" && targetLayer > sourceLayer + 1)
                        reasons.Add(rule.Message ?? $"Strict Layering Violation: {GetLayerName(sourceLayer, layering)} cannot skip layers to {GetLayerName(targetLayer, layering)}");
                }
            }
            else if (rule is VisibilityRule visibility)
            {
                if (visibility.TargetRaw != null && Matches(target, visibility.TargetRaw))
                {
                    bool isAllowed = false;
                    foreach (var consumer in visibility.AllowedConsumersRaw)
                    {
                        if (Matches(source, consumer))
                        {
                            isAllowed = true;
                            break;
                        }
                    }
                    
                    if (!isAllowed)
                    {
                        reasons.Add(rule.Message ?? $"Visibility Violation: {target.Name} is restricted. {source.Name} is not an allowed consumer.");
                    }
                }
            }
        }

        return reasons;
    }

    // --- Matching Logic ---

    private bool Matches(CodeAtom atom, object? selectorRaw)
    {
        if (selectorRaw == null) return false;

        if (selectorRaw is string refStr && refStr.StartsWith("@"))
        {
            var defKey = refStr.Substring(1);
            if (_resolvedDefinitions.TryGetValue(defKey, out var def))
            {
                return MatchesSelector(atom, def);
            }
        }
        // Handle inline objects if needed (skipped for simplicity)
        return false;
    }

    private bool MatchesSelector(CodeAtom atom, AtomSelector selector)
    {
        if (selector.Pattern != null)
        {
            if (!_compiledPatterns.TryGetValue(selector.Pattern, out var regex))
            {
                regex = new Regex(selector.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                _compiledPatterns[selector.Pattern] = regex;
            }
            if (!regex.IsMatch(atom.Name)) return false;
        }

        if (selector.Namespace != null)
        {
            // Simple wildcard support
            var nsPattern = "^" + Regex.Escape(selector.Namespace).Replace("\\*", ".*") + "$";
            if (!Regex.IsMatch(atom.Namespace, nsPattern)) return false;
        }

        if (selector.Type != null)
        {
            if (!atom.Type.ToString().Equals(selector.Type, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private int GetLayerIndex(CodeAtom atom, List<object> layers)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (Matches(atom, layers[i])) return i;
        }
        return -1;
    }
    
    private string GetLayerName(int index, LayeringRule rule)
    {
        if (index >= 0 && index < rule.LayersRaw.Count)
        {
            return rule.LayersRaw[index].ToString() ?? $"Layer {index}"; 
        }
        return "Unknown Layer";
    }
}
