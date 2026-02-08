using HotChocolate.Types;
using DiagnosticStructuralLens.Graph;

namespace DiagnosticStructuralLens.Api.GraphQL;

/// <summary>
/// HotChocolate type definitions for GraphQL schema.
/// </summary>

public class FederationViewType : ObjectType<FederationView>
{
    protected override void Configure(IObjectTypeDescriptor<FederationView> descriptor)
    {
        descriptor.Description("L1: Context - Federated view of all repositories");
        descriptor.Field(f => f.Id).Description("Federation ID");
        descriptor.Field(f => f.FederatedAt).Description("When federation was created");
        descriptor.Field(f => f.Repositories).Description("All federated repositories");
        descriptor.Field(f => f.CrossRepoLinks).Description("Links that span repositories");
        descriptor.Field(f => f.Stats).Description("Aggregate statistics");
    }
}

public class RepositoryNodeType : ObjectType<RepositoryNode>
{
    protected override void Configure(IObjectTypeDescriptor<RepositoryNode> descriptor)
    {
        descriptor.Description("A repository in the federation (L1 node)");
        descriptor.Field(f => f.Id).Description("Repository identifier");
        descriptor.Field(f => f.Name).Description("Repository name");
        descriptor.Field(f => f.AtomCount).Description("Total atoms in this repository");
        descriptor.Field(f => f.RiskScore).Description("Aggregate risk score (0-100)");
        descriptor.Field(f => f.Namespaces).Description("Namespace paths in this repository");
        descriptor.Field(f => f.Owner).Description("Repository owner");
        descriptor.Field(f => f.QualityMetrics).Description("Quality metrics");
        descriptor.Field(f => f.ChurnScore).Description("Commit frequency score (0-100)");
        descriptor.Field(f => f.MaintenanceCost).Description("Estimated maintenance effort (0-100)");
    }
}

public class OwnerType : ObjectType<Owner>
{
    protected override void Configure(IObjectTypeDescriptor<Owner> descriptor)
    {
        descriptor.Description("Repository owner information");
        descriptor.Field(f => f.Name).Description("The name of the owner");
        descriptor.Field(f => f.Email).Description("The email of the owner");
        descriptor.Field(f => f.TeamName).Description("The team name of the owner");
        descriptor.Field(f => f.AvatarUrl).Description("The avatar URL of the owner");
    }
}

public class QualityMetricsType : ObjectType<QualityMetrics>
{
    protected override void Configure(IObjectTypeDescriptor<QualityMetrics> descriptor)
    {
        descriptor.Description("Quality metrics for a repository");
        descriptor.Field(f => f.CoveragePercent).Description("Code coverage percentage");
        descriptor.Field(f => f.SonarRating).Description("SonarQube rating (A-E)");
        descriptor.Field(f => f.CyclomaticComplexity).Description("Average cyclomatic complexity");
    }
}

public class NamespaceNodeType : ObjectType<NamespaceNode>
{
    protected override void Configure(IObjectTypeDescriptor<NamespaceNode> descriptor)
    {
        descriptor.Description("A namespace cluster within a repository (L2 node)");
        descriptor.Field(f => f.Path).Description("Full namespace path");
        descriptor.Field(f => f.AtomCount).Description("Total atoms in namespace");
        descriptor.Field(f => f.DtoCount).Description("DTOs in namespace");
        descriptor.Field(f => f.InterfaceCount).Description("Interfaces in namespace");
    }
}

public class NamespaceViewType : ObjectType<NamespaceView>
{
    protected override void Configure(IObjectTypeDescriptor<NamespaceView> descriptor)
    {
        descriptor.Description("L3: Component - Namespace with atoms and internal links");
        descriptor.Field(f => f.Path).Description("Namespace path");
        descriptor.Field(f => f.Atoms).Description("Atoms in this namespace");
        descriptor.Field(f => f.InternalLinks).Description("Links between atoms in this namespace");
    }
}

public class InternalLinkType : ObjectType<InternalLink>
{
    protected override void Configure(IObjectTypeDescriptor<InternalLink> descriptor)
    {
        descriptor.Description("A link between atoms within the same namespace");
        descriptor.Field(f => f.SourceAtomId).Description("Source atom ID");
        descriptor.Field(f => f.TargetAtomId).Description("Target atom ID");
        descriptor.Field(f => f.LinkType).Description("Type of link");
        descriptor.Field(f => f.LinkType).Description("Type of link");
        descriptor.Field(f => f.IsViolation).Description("Whether this link violates architectural rules");
        descriptor.Field(f => f.ViolationDetails).Description("Details of the violation if present");
    }
}

public class ViolationDetailsType : ObjectType<ViolationDetails>
{
    protected override void Configure(IObjectTypeDescriptor<ViolationDetails> descriptor)
    {
        descriptor.Description("Details about an architectural violation");
        descriptor.Field(f => f.RuleId).Description("ID of the violated rule");
        descriptor.Field(f => f.Severity).Description("Severity of the violation (Critical, Warning)");
        descriptor.Field(f => f.Message).Description("Description of the violation");
        descriptor.Field(f => f.RemediationSuggestion).Description("Suggested fix");
    }
}

public class AtomNodeType : ObjectType<AtomNode>
{
    protected override void Configure(IObjectTypeDescriptor<AtomNode> descriptor)
    {
        descriptor.Description("An atom (code or SQL element) at L3 level");
        descriptor.Field(f => f.Id).Description("Unique atom identifier");
        descriptor.Field(f => f.Name).Description("Atom name");
        descriptor.Field(f => f.Type).Description("Atom type (DTO, Interface, Table, etc.)");
        descriptor.Field(f => f.RiskScore).Description("Risk score (0-100)");
        descriptor.Field(f => f.ConsumerCount).Description("Number of atoms that depend on this");
        descriptor.Field(f => f.LinesOfCode).Description("Number of lines of code in this atom");
        descriptor.Field(f => f.Language).Description("Programming language (csharp, sql, etc.)");
        descriptor.Field(f => f.IsPublic).Description("Whether the atom is public");
        descriptor.Field(f => f.ChurnScore).Description("Commit frequency score (0-100)");
        descriptor.Field(f => f.MaintenanceCost).Description("Estimated maintenance effort (0-100)");
    }
}

public class AtomDetailType : ObjectType<AtomDetail>
{
    protected override void Configure(IObjectTypeDescriptor<AtomDetail> descriptor)
    {
        descriptor.Description("L4: Code - Detailed atom information");
        descriptor.Field(f => f.Id).Description("Unique atom identifier");
        descriptor.Field(f => f.Name).Description("Atom name");
        descriptor.Field(f => f.Type).Description("Atom type");
        descriptor.Field(f => f.Namespace).Description("C# namespace (for code atoms)");
        descriptor.Field(f => f.FilePath).Description("Source file path");
        descriptor.Field(f => f.ParentTable).Description("Parent table (for SQL columns)");
        descriptor.Field(f => f.DataType).Description("Data type (for columns)");
        descriptor.Field(f => f.Repository).Description("Source repository");
        descriptor.Field(f => f.LinesOfCode).Description("Number of lines of code");
        descriptor.Field(f => f.Language).Description("Programming language");
        descriptor.Field(f => f.IsPublic).Description("Whether the atom is public");
        descriptor.Field(f => f.Members).Description("Methods, properties, and fields");
        descriptor.Field(f => f.InboundLinks).Description("Atoms that reference this one");
        descriptor.Field(f => f.OutboundLinks).Description("Atoms this one references");
    }
}

public class BlastRadiusResultType : ObjectType<BlastRadiusResult>
{
    protected override void Configure(IObjectTypeDescriptor<BlastRadiusResult> descriptor)
    {
        descriptor.Description("Blast radius calculation result");
        descriptor.Field(f => f.SourceAtomId).Description("The atom change would originate from");
        descriptor.Field(f => f.AffectedAtoms).Description("All transitively affected atoms");
        descriptor.Field(f => f.TotalAffected).Description("Total count of affected atoms");
        descriptor.Field(f => f.ByDepth).Description("Affected atoms grouped by hop distance");
    }
}

public class GraphNodeType : ObjectType<GraphNode>
{
    protected override void Configure(IObjectTypeDescriptor<GraphNode> descriptor)
    {
        descriptor.Description("A node in the Knowledge Graph");
        descriptor.Field(f => f.Id).Description("Unique identifier");
        descriptor.Field(f => f.Name).Description("Node name");
        descriptor.Field(f => f.Type).Description("Node type");
        descriptor.Field(f => f.Properties).Description("Arbitrary properties").Type<AnyType>();
    }
}

public class GraphEdgeType : ObjectType<GraphEdge>
{
    protected override void Configure(IObjectTypeDescriptor<GraphEdge> descriptor)
    {
        descriptor.Description("An edge in the Knowledge Graph");
        descriptor.Field(f => f.Id).Description("Unique edge identifier");
        descriptor.Field(f => f.SourceId).Description("Source node ID");
        descriptor.Field(f => f.TargetId).Description("Target node ID");
        descriptor.Field(f => f.Type).Description("Edge type");
        descriptor.Field(f => f.Properties).Description("Arbitrary properties").Type<AnyType>();
    }
}

public class TraversalResultType : ObjectType<TraversalResult>
{
    protected override void Configure(IObjectTypeDescriptor<TraversalResult> descriptor)
    {
        descriptor.Description("Result of a graph traversal");
        descriptor.Field(f => f.StartNode).Description("Starting node");
        descriptor.Field(f => f.Levels).Description("Traversal levels");
        descriptor.Field(f => f.TotalNodesFound).Description("Total nodes found");
    }
}

public class TraversalLevelType : ObjectType<TraversalLevel>
{
    protected override void Configure(IObjectTypeDescriptor<TraversalLevel> descriptor)
    {
        descriptor.Description("Nodes found at a specific depth");
        descriptor.Field(f => f.Depth).Description("Depth level");
        descriptor.Field(f => f.Hits).Description("Nodes found at this level");
    }
}

public class TraversalHitType : ObjectType<TraversalHit>
{
    protected override void Configure(IObjectTypeDescriptor<TraversalHit> descriptor)
    {
        descriptor.Description("A node hit during traversal");
        descriptor.Field(f => f.Node).Description("The node found");
        descriptor.Field(f => f.ViaEdge).Description("The edge followed to get here");
        descriptor.Field(f => f.FromNode).Description("The previous node");
    }
}

public class GraphCycleType : ObjectType<GraphCycle>
{
    protected override void Configure(IObjectTypeDescriptor<GraphCycle> descriptor)
    {
        descriptor.Description("A detected cycle in the graph");
        descriptor.Field(f => f.Nodes).Description("Nodes involved in the cycle");
        descriptor.Field(f => f.Severity).Description("Severity of the cycle");
    }
}
