namespace DiagnosticStructuralLens.Core;

/// <summary>
/// Represents a code-level atomic element (class, interface, method, property).
/// </summary>
public record CodeAtom
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required AtomType Type { get; init; }
    public required string Namespace { get; init; }
    public string? Repository { get; init; }
    public string? Signature { get; init; }
    public string? TargetFramework { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public int? LinesOfCode { get; init; }
    public string? Language { get; init; }
    public bool IsPublic { get; init; }
}

/// <summary>
/// Represents a SQL-level atomic element (table, column, stored procedure).
/// </summary>
public record SqlAtom
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SqlAtomType Type { get; init; }
    public string? ParentTable { get; init; }
    public string? DataType { get; init; }
    public bool IsNullable { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
}

/// <summary>
/// Represents a relationship between two atoms.
/// </summary>
public record AtomLink
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required LinkType Type { get; init; }
    public double Confidence { get; init; } = 1.0;
    public string? Evidence { get; init; }
}

public enum AtomType
{
    Class,
    Interface,
    Record,
    Struct,
    Enum,
    Method,
    Property,
    Field,
    Dto,
    Unknown,
    
    // TypeScript elements
    TypeAlias,
    Module,
    Component
}

public enum SqlAtomType
{
    Table,
    Column,
    StoredProcedure,
    View,
    Function,
    Index
}

public enum LinkType
{
    // Code relationships
    Inherits,
    Implements,
    Calls,
    References,
    Contains,
    
    // Cross-domain links (C# ↔ SQL)
    NameMatch,
    AttributeBinding,
    QueryTrace,
    
    // Semantic linker match types
    ExactMatch,
    FuzzyMatch,
    PropertyMatch,
    
    // Package relationships
    PackageDependency,
    ProjectReference,
    
    // TypeScript relationships
    Imports,
    ReExports,
    WorkspaceDependency
}
