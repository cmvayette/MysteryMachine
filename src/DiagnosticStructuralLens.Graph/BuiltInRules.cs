namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Provides standard architectural rules.
/// </summary>
public static class BuiltInRules
{
    public static IReadOnlyList<ArchitectureRule> All => new[]
    {
        // C# rules
        NoControllerToRepository,
        NoDomainToInfrastructure,
        // TypeScript rules
        NoCrossDomainImport,
        NoDeepPackageImport,
        NoCircularWorkspaceDep,
        NoUiToBackendImport
    };

    // ── C# Rules ──────────────────────────────────────────────────────

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
        ForbiddenEdge = EdgeType.DependsOn,
        Target = new NodeQuery 
        { 
            NamespacePattern = "*.Infrastructure*" 
        }
    };

    // ── TypeScript Rules ──────────────────────────────────────────────

    /// <summary>
    /// TS001: Code in one domain must not import from another domain directly.
    /// Uses SameGroupExclusion to allow imports within the same domain.
    /// Example violation: domains/admin → imports → domains/governance
    /// </summary>
    public static ArchitectureRule NoCrossDomainImport => new ArchitectureRule
    {
        Id = "TS001",
        Name = "No Cross-Domain Import",
        Description = "Domain modules must not import from other domains directly. Use shared packages or barrel exports.",
        Severity = RuleSeverity.Error,
        Source = new NodeQuery
        {
            NamespacePattern = "*::domains.*"
        },
        ForbiddenEdge = EdgeType.Imports,
        Target = new NodeQuery
        {
            NamespacePattern = "*::domains.*"
        },
        SameGroupExclusion = new GroupExclusion
        {
            PrimarySeparator = "::",
            SegmentIndex = 1,        // "domains.governance.utils" part
            SecondarySeparator = ".",
            Depth = 2                // "domains.governance" = same group
        }
    };

    /// <summary>
    /// TS002: External consumers should import through barrel index.ts, not reach into internal modules.
    /// Catches imports of non-public (non-exported) symbols across workspace boundaries.
    /// </summary>
    public static ArchitectureRule NoDeepPackageImport => new ArchitectureRule
    {
        Id = "TS002",
        Name = "No Deep Package Import",
        Description = "Import through barrel exports (index.ts), not directly into internal modules.",
        Severity = RuleSeverity.Warning,
        Source = new NodeQuery
        {
            NamespacePattern = "*"  // Any source
        },
        ForbiddenEdge = EdgeType.Imports,
        Target = new NodeQuery
        {
            IsPublic = false,
            NamespacePattern = "*::*"  // Only TS namespaces (with ::)
        },
        // Exclude same-workspace imports (internal imports are fine)
        SameGroupExclusion = new GroupExclusion
        {
            PrimarySeparator = "::",
            SegmentIndex = 0,        // Workspace name part
            SecondarySeparator = "::",
            Depth = 1                // Same workspace = same group
        }
    };

    /// <summary>
    /// TS003: Workspaces must not form circular dependencies.
    /// Detects workspace A → depends on → workspace B where B also depends on A.
    /// Note: This catches the "forward" direction; the reverse is caught symmetrically.
    /// </summary>
    public static ArchitectureRule NoCircularWorkspaceDep => new ArchitectureRule
    {
        Id = "TS003",
        Name = "No Circular Workspace Dependency",
        Description = "Workspace packages must form a DAG. Circular dependencies indicate coupling.",
        Severity = RuleSeverity.Error,
        Source = new NodeQuery
        {
            Type = NodeType.Module
        },
        ForbiddenEdge = EdgeType.WorkspaceDep,
        Target = new NodeQuery
        {
            Type = NodeType.Module
        }
    };

    /// <summary>
    /// TS004: Frontend/UI packages must not import backend domain code directly.
    /// They should go through the API layer (@som/api-client).
    /// </summary>
    public static ArchitectureRule NoUiToBackendImport => new ArchitectureRule
    {
        Id = "TS004",
        Name = "No UI -> Backend Import",
        Description = "UI packages must access backend domains through the API client, not via direct imports.",
        Severity = RuleSeverity.Error,
        Source = new NodeQuery
        {
            NamespacePattern = "unified-shell*"
        },
        ForbiddenEdge = EdgeType.Imports,
        Target = new NodeQuery
        {
            NamespacePattern = "semantic-operating-model::domains.*"
        }
    };
}
