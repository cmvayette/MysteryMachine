using System.Text.Json;
using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Graph;
using Xunit;

namespace DiagnosticStructuralLens.Tests;

/// <summary>
/// Phase 3 Verification: Rule Engine
/// </summary>
public class RuleEngineTests
{
    private readonly RuleEngine _engine;
    private readonly KnowledgeGraph _graph;

    public RuleEngineTests()
    {
        _graph = new KnowledgeGraph { Id = "test-graph" };
        _engine = new RuleEngine(_graph);
    }

    #region Rule Evaluation Logic

    [Fact]
    public void EvaluateRule_DetectsForbiddenEdge()
    {
        // Setup: A -> B
        var a = CreateNode("Source", "MySource", NodeType.Class);
        var b = CreateNode("Target", "MyTarget", NodeType.Class);
        AddEdge(a, b, EdgeType.DependsOn);

        var rule = new ArchitectureRule
        {
            Id = "TEST001",
            Name = "No Source -> Target",
            Description = "Test rule",
            Severity = RuleSeverity.Error,
            Source = new NodeQuery { NamePattern = "MySource" },
            ForbiddenEdge = EdgeType.DependsOn,
            Target = new NodeQuery { NamePattern = "MyTarget" }
        };

        // Act
        var violations = _engine.EvaluateRule(rule);

        // Assert
        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal("Source", v.Source.Id);
        Assert.Equal("Target", v.Target.Id);
        Assert.Equal(rule, v.Rule);
    }

    [Fact]
    public void EvaluateRule_IgnoresAllowedEdges()
    {
        // Setup: A -> B (UsesPackage, not DependsOn)
        var a = CreateNode("Source", "MySource", NodeType.Class);
        var b = CreateNode("Target", "MyTarget", NodeType.Class);
        AddEdge(a, b, EdgeType.UsesPackage);

        var rule = new ArchitectureRule
        {
            Id = "TEST001",
            Name = "No Source -> Target",
            Description = "Test rule",
            Severity = RuleSeverity.Error,
            Source = new NodeQuery { NamePattern = "MySource" },
            ForbiddenEdge = EdgeType.DependsOn, // Only detecting DependsOn
            Target = new NodeQuery { NamePattern = "MyTarget" }
        };

        // Act
        var violations = _engine.EvaluateRule(rule);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void EvaluateRule_RespectsWildcards()
    {
        // Setup: OrderController -> OrderRepository
        var ctrl = CreateNode("Ctrl", "OrderController", NodeType.Class);
        var repo = CreateNode("Repo", "OrderRepository", NodeType.Class);
        AddEdge(ctrl, repo, EdgeType.DependsOn);

        var rule = new ArchitectureRule
        {
            Id = "TEST002",
            Name = "Wildcards",
            Description = "Test wildcards",
            Severity = RuleSeverity.Error,
            Source = new NodeQuery { NamePattern = "*Controller" }, // Match suffix
            ForbiddenEdge = EdgeType.DependsOn,
            Target = new NodeQuery { NamePattern = "*Repository" }  // Match suffix
        };

        // Act
        var violations = _engine.EvaluateRule(rule);

        // Assert
        Assert.Single(violations);
    }

    [Fact]
    public void EvaluateRule_RespectsNamespacePatterns()
    {
        // Setup: Domain.Service -> Infra.Db
        var svc = CreateNode("Svc", "Service", NodeType.Class, "MyApp.Domain.Services");
        var db = CreateNode("Db", "Database", NodeType.Class, "MyApp.Infrastructure.Data");
        AddEdge(svc, db, EdgeType.DependsOn);

        var rule = new ArchitectureRule
        {
            Id = "TEST003",
            Name = "Layering",
            Description = "Domain -> Infra forbidden",
            Severity = RuleSeverity.Error,
            Source = new NodeQuery { NamespacePattern = "*.Domain.*" },
            ForbiddenEdge = EdgeType.DependsOn,
            Target = new NodeQuery { NamespacePattern = "*.Infrastructure.*" }
        };

        // Act
        var violations = _engine.EvaluateRule(rule);

        // Assert
        Assert.Single(violations);
    }

    #endregion

    #region Built-in Rules (DoD #1, #2)

    [Fact]
    public void BuiltInRules_ControllerToRepo_Fails()
    {
        var ctrl = CreateNode("C", "UserController", NodeType.Class);
        var repo = CreateNode("R", "UserRepository", NodeType.Class);
        AddEdge(ctrl, repo, EdgeType.DependsOn);

        var rule = BuiltInRules.NoControllerToRepository;
        var violations = _engine.EvaluateRule(rule);

        Assert.Single(violations);
    }

    [Fact]
    public void BuiltInRules_DomainToInfra_Fails()
    {
        var dom = CreateNode("D", "Order", NodeType.Class, "Shop.Domain");
        var infra = CreateNode("I", "OrderContext", NodeType.Class, "Shop.Infrastructure");
        AddEdge(dom, infra, EdgeType.DependsOn);

        var rule = BuiltInRules.NoDomainToInfrastructure;
        var violations = _engine.EvaluateRule(rule);

        Assert.Single(violations);
    }

    #endregion

    #region Rule Loader (DoD #3)

    [Fact]
    public void RuleLoader_ParsesJsonCorrectly()
    {
        var json = @"
        [
            {
                ""Id"": ""CUSTOM01"",
                ""Name"": ""No Cycles"",
                ""Description"": ""Custom rule"",
                ""Severity"": ""Warning"",
                ""Source"": { ""Type"": ""Class"" },
                ""ForbiddenEdge"": ""References"",
                ""Target"": { ""Type"": ""Interface"" }
            }
        ]";

        var loader = new RuleLoader();
        var rules = loader.LoadRules(json);

        Assert.Contains(rules, r => r.Id == "CUSTOM01");
        var custom = rules.First(r => r.Id == "CUSTOM01");
        Assert.Equal(RuleSeverity.Warning, custom.Severity);
        Assert.Equal(NodeType.Class, custom.Source.Type);
    }

    [Fact]
    public void RuleLoader_OverridesBuiltIn()
    {
        // Override ARCH001 to be Info instead of Error
        var json = @"
        [
            {
                ""Id"": ""ARCH001"",
                ""Name"": ""No Controller -> Repository (Relaxed)"",
                ""Description"": ""Overridden"",
                ""Severity"": ""Info"",
                ""Source"": { ""NamePattern"": ""*Controller"" },
                ""ForbiddenEdge"": ""DependsOn"",
                ""Target"": { ""NamePattern"": ""*Repository"" }
            }
        ]";

        var loader = new RuleLoader();
        var rules = loader.LoadRules(json);

        var validRules = rules.Where(r => r.Id == "ARCH001").ToList();
        Assert.Single(validRules); // Should replace, not append
        Assert.Equal(RuleSeverity.Info, validRules[0].Severity);
    }

    #endregion

    #region Helpers

    private GraphNode CreateNode(string id, string name, NodeType type, string ns = "")
    {
        var node = new GraphNode
        {
            Id = id,
            Name = name,
            Type = type,
            Properties = new Dictionary<string, object>()
        };
        if (!string.IsNullOrEmpty(ns))
        {
            node.Properties["Namespace"] = ns;
        }
        
        // Needed for direct adding since we usually use builder
        _graph.AddNode(node);
        _graph.BuildIndexes(); // Ensure type index is updated
        return node;
    }

    private void AddEdge(GraphNode source, GraphNode target, EdgeType type)
    {
        var edge = new GraphEdge
        {
            Id = $"{source.Id}->{target.Id}",
            SourceId = source.Id,
            TargetId = target.Id,
            Type = type
        };
        _graph.AddEdge(edge);
        
        // Internal methods are visible now.
        // MUST build indexes first, then populate navigation
        _graph.BuildIndexes(); 
        _graph.PopulateNavigation();
    }

    #endregion
}
