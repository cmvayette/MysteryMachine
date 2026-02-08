namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Provides standard architectural rules.
/// </summary>
public static class BuiltInRules
{
    public static IReadOnlyList<ArchitectureRule> All => new[]
    {
        NoControllerToRepository,
        NoDomainToInfrastructure
    };

    /// <summary>
    /// ARCH001: Controllers must not depend directly on Repositories (should use Services/Mediators).
    /// </summary>
    public static ArchitectureRule NoControllerToRepository => new ArchitectureRule
    {
        Id = "ARCH001",
        Name = "No Controller -> Repository",
        Description = "Controllers should not access Repositories directly. Use a Service layer.",
        Severity = RuleSeverity.Error,
        Source = new NodeQuery 
        { 
            Type = NodeType.Class, 
            NamePattern = "*Controller" 
        },
        ForbiddenEdge = EdgeType.DependsOn,
        Target = new NodeQuery 
        { 
            Type = NodeType.Class, 
            NamePattern = "*Repository" 
        }
    };

    /// <summary>
    /// ARCH002: Domain layer must not depend on Infrastructure layer.
    /// </summary>
    public static ArchitectureRule NoDomainToInfrastructure => new ArchitectureRule
    {
        Id = "ARCH002",
        Name = "No Domain -> Infrastructure",
        Description = "Domain entities must remain pure and not depend on infrastructure.",
        Severity = RuleSeverity.Error,
        Source = new NodeQuery 
        { 
            NamespacePattern = "*.Domain*" 
        },
        ForbiddenEdge = EdgeType.DependsOn, // Also References check? Usually generic 'DependsOn' covers it in Graph map.
        Target = new NodeQuery 
        { 
            NamespacePattern = "*.Infrastructure*" 
        }
    };
}
