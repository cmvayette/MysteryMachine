namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Robert C. Martin's Package Metrics for a single namespace/package.
/// </summary>
public record NamespaceMetrics
{
    /// <summary>The namespace being measured.</summary>
    public required string Namespace { get; init; }
    
    /// <summary>Total type-level nodes (Class, Interface, Enum, etc.)</summary>
    public int TotalTypes { get; init; }
    
    /// <summary>Abstract types (Interfaces, Enums, TypeAliases).</summary>
    public int AbstractTypes { get; init; }
    
    /// <summary>Ca: Incoming edges from outside this namespace.</summary>
    public int AfferentCoupling { get; init; }
    
    /// <summary>Ce: Outgoing edges to outside this namespace.</summary>
    public int EfferentCoupling { get; init; }
    
    /// <summary>I = Ce / (Ca + Ce). 0 = maximally stable, 1 = maximally unstable.</summary>
    public double Instability { get; init; }
    
    /// <summary>A = AbstractTypes / TotalTypes. 0 = all concrete, 1 = all abstract.</summary>
    public double Abstractness { get; init; }
    
    /// <summary>D = |A + I - 1|. 0 = on the Main Sequence, >0.5 = problematic.</summary>
    public double DistanceFromMainSequence { get; init; }
    
    /// <summary>"Ideal", "Pain" (stable+concrete), or "Uselessness" (unstable+abstract).</summary>
    public string Zone { get; init; } = "";
}
