using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Federation;

namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Transforms a Snapshot into a KnowledgeGraph.
/// </summary>
public class GraphBuilder
{
    /// <summary>
    /// Build a knowledge graph from a snapshot.
    /// </summary>
    /// <summary>
    /// Build a knowledge graph from a federated snapshot (merged view).
    /// </summary>
    public KnowledgeGraph Build(FederatedSnapshot snapshot)
    {
        var graph = new KnowledgeGraph
        {
            Id = $"graph-{snapshot.Id}",
            Repository = "Federated", // Can be customized
            CreatedAt = snapshot.FederatedAt,
            GitBranch = "federated"
        };
        
        // 1. Convert Federated Atoms to GraphNodes
        foreach (var fedAtom in snapshot.CodeAtoms)
        {
            var node = MapCodeAtomToNode(fedAtom.Atom);
            // Enrich with source repo
            node.Properties["SourceRepo"] = fedAtom.SourceRepo;
            graph.AddNode(node);
        }
        
        foreach (var fedAtom in snapshot.SqlAtoms)
        {
            var node = MapSqlAtomToNode(fedAtom.Atom);
            node.Properties["SourceRepo"] = fedAtom.SourceRepo;
            graph.AddNode(node);
        }
        
        // 2. Convert Linksto GraphEdges
        foreach (var fedLink in snapshot.Links)
        {
            var edge = MapLinkToEdge(fedLink.Link);
            edge.Properties["SourceRepo"] = fedLink.SourceRepo;
            edge.Properties["TargetRepo"] = fedLink.TargetRepo;
            graph.AddEdge(edge);
        }
        
        // 3. Build indexes
        graph.BuildIndexes();
        
        // 4. Populate navigation
        graph.PopulateNavigation();
        
        return graph;
    }

    /// <summary>
    /// Build a knowledge graph from a snapshot.
    /// </summary>
    public KnowledgeGraph Build(Snapshot snapshot)
    {
        var graph = new KnowledgeGraph
        {
            Id = $"graph-{snapshot.Id}",
            Repository = snapshot.Repository,
            CreatedAt = snapshot.ScannedAt,
            GitCommit = snapshot.CommitSha,
            GitBranch = snapshot.Branch
        };
        
        // 1. Convert CodeAtoms to GraphNodes
        foreach (var atom in snapshot.CodeAtoms)
        {
            var node = MapCodeAtomToNode(atom);
            graph.AddNode(node);
        }
        
        // 2. Convert SqlAtoms to GraphNodes
        foreach (var atom in snapshot.SqlAtoms)
        {
            var node = MapSqlAtomToNode(atom);
            graph.AddNode(node);
        }
        
        // 3. Convert AtomLinks to GraphEdges
        foreach (var link in snapshot.Links)
        {
            var edge = MapLinkToEdge(link);
            graph.AddEdge(edge);
        }
        
        // 4. Build indexes for O(1) lookups
        graph.BuildIndexes();
        
        // 5. Populate navigation properties
        graph.PopulateNavigation();
        
        return graph;
    }
    
    private static GraphNode MapCodeAtomToNode(CodeAtom atom)
    {
        var properties = new Dictionary<string, object>
        {
            ["Namespace"] = atom.Namespace,
            ["IsPublic"] = atom.IsPublic
        };
        
        if (atom.Signature is not null)
            properties["Signature"] = atom.Signature;
        
        if (atom.Repository is not null)
            properties["Repository"] = atom.Repository;
        
        if (atom.TargetFramework is not null)
            properties["TargetFramework"] = atom.TargetFramework;
        
        if (atom.Language is not null)
            properties["Language"] = atom.Language;
        
        if (atom.LinesOfCode.HasValue)
            properties["LinesOfCode"] = atom.LinesOfCode.Value;
        
        return new GraphNode
        {
            Id = atom.Id,
            Name = atom.Name,
            Type = MapAtomType(atom.Type),
            FilePath = atom.FilePath,
            LineNumber = atom.LineNumber,
            Properties = properties
        };
    }
    
    private static GraphNode MapSqlAtomToNode(SqlAtom atom)
    {
        var properties = new Dictionary<string, object>();
        
        if (atom.ParentTable is not null)
            properties["ParentTable"] = atom.ParentTable;
        
        if (atom.DataType is not null)
            properties["DataType"] = atom.DataType;
        
        properties["IsNullable"] = atom.IsNullable;
        
        return new GraphNode
        {
            Id = atom.Id,
            Name = atom.Name,
            Type = MapSqlAtomType(atom.Type),
            FilePath = atom.FilePath,
            Properties = properties
        };
    }
    
    private static GraphEdge MapLinkToEdge(AtomLink link)
    {
        var properties = new Dictionary<string, object>
        {
            ["Confidence"] = link.Confidence
        };
        
        if (link.Evidence is not null)
            properties["Evidence"] = link.Evidence;
        
        return new GraphEdge
        {
            Id = link.Id,
            SourceId = link.SourceId,
            TargetId = link.TargetId,
            Type = MapLinkType(link.Type),
            Properties = properties
        };
    }
    
    private static NodeType MapAtomType(AtomType atomType) => atomType switch
    {
        AtomType.Class => NodeType.Class,
        AtomType.Interface => NodeType.Interface,
        AtomType.Record => NodeType.Record,
        AtomType.Struct => NodeType.Struct,
        AtomType.Enum => NodeType.Enum,
        AtomType.Method => NodeType.Method,
        AtomType.Property => NodeType.Property,
        AtomType.Field => NodeType.Field,
        AtomType.Dto => NodeType.Class, // DTOs are classes with a marker
        AtomType.Unknown => NodeType.Class, // Default fallback
        _ => NodeType.Class
    };
    
    private static NodeType MapSqlAtomType(SqlAtomType sqlType) => sqlType switch
    {
        SqlAtomType.Table => NodeType.Table,
        SqlAtomType.Column => NodeType.Column,
        SqlAtomType.StoredProcedure => NodeType.StoredProcedure,
        SqlAtomType.View => NodeType.View,
        SqlAtomType.Function => NodeType.StoredProcedure, // Treat functions as procs
        SqlAtomType.Index => NodeType.Table, // Indexes belong to tables
        _ => NodeType.Table
    };
    
    private static EdgeType MapLinkType(LinkType linkType) => linkType switch
    {
        LinkType.Inherits => EdgeType.Inherits,
        LinkType.Implements => EdgeType.Implements,
        LinkType.Calls => EdgeType.Calls,
        LinkType.References => EdgeType.DependsOn,
        LinkType.Contains => EdgeType.Contains,
        LinkType.NameMatch => EdgeType.NameMatch,
        LinkType.AttributeBinding => EdgeType.AttributeBinding,
        LinkType.QueryTrace => EdgeType.QueryTrace,
        LinkType.ExactMatch => EdgeType.NameMatch,
        LinkType.FuzzyMatch => EdgeType.NameMatch,
        LinkType.PropertyMatch => EdgeType.NameMatch,
        LinkType.PackageDependency => EdgeType.UsesPackage,
        LinkType.ProjectReference => EdgeType.References,
        _ => EdgeType.DependsOn
    };
}
