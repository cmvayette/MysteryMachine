using HotChocolate.Types;

namespace SystemCartographer.Api.GraphQL;

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
