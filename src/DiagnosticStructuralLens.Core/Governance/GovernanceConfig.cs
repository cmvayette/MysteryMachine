using YamlDotNet.Serialization;

namespace DiagnosticStructuralLens.Core.Governance;

public class GovernanceConfig
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "1.0";

    [YamlMember(Alias = "definitions")]
    public Dictionary<string, AtomSelector>? Definitions { get; set; }

    [YamlMember(Alias = "rules")]
    public List<GovernanceRule> Rules { get; set; } = new();
}

public class AtomSelector
{
    [YamlMember(Alias = "pattern")]
    public string? Pattern { get; set; }

    [YamlMember(Alias = "namespace")]
    public string? Namespace { get; set; }

    [YamlMember(Alias = "type")]
    public string? Type { get; set; }
}

// Polymorphic Base
public class GovernanceRule
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "unknown";

    [YamlMember(Alias = "severity")]
    public string Severity { get; set; } = "error"; // "error", "warning", "info"
    
    [YamlMember(Alias = "message")]
    public string? Message { get; set; }
}

// Forbidden: Source cannot touch Target
public class ForbiddenRule : GovernanceRule
{
    [YamlMember(Alias = "source")]
    public object? SourceRaw { get; set; } // Can be string (@ref) or AtomSelector object

    [YamlMember(Alias = "target")]
    public object? TargetRaw { get; set; } // Can be string (@ref) or AtomSelector object
}

// Layering: Layers must be respected (Top -> Bottom)
public class LayeringRule : GovernanceRule
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; set; } = "strict"; // "strict" or "relaxed"

    [YamlMember(Alias = "layers")]
    public List<object> LayersRaw { get; set; } = new(); // List of strings (@ref) or AtomSelector objects
}

public class VisibilityRule : GovernanceRule
{
    [YamlMember(Alias = "target")]
    public object? TargetRaw { get; set; } 

    [YamlMember(Alias = "allowed_consumers")]
    public List<object> AllowedConsumersRaw { get; set; } = new();
}

