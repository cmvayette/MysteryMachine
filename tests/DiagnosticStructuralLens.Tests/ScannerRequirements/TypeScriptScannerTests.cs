using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Scanner.TypeScript;
using Xunit;

namespace DiagnosticStructuralLens.Tests.ScannerRequirements;

/// <summary>
/// Tests for the TypeScript scanner C# integration bridge.
/// These verify the IScanner contract, JSON deserialization, and subprocess invocation.
/// </summary>
public class TypeScriptScannerTests
{
    [Fact]
    public void TypeScriptScanner_Implements_IScanner()
    {
        var scanner = new TypeScriptScanner();
        Assert.IsAssignableFrom<IScanner>(scanner);
    }

    [Fact]
    public async Task ScanAsync_Returns_Error_For_Missing_Path()
    {
        var scanner = new TypeScriptScanner();
        var result = await scanner.ScanAsync("/nonexistent/path/that/does/not/exist");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        Assert.Contains("not found", result.Diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanAsync_Returns_Empty_Lists_By_Default()
    {
        var scanner = new TypeScriptScanner();
        var result = await scanner.ScanAsync("/nonexistent/path/that/does/not/exist");

        // Should have empty atom/link lists even on error
        Assert.NotNull(result.CodeAtoms);
        Assert.NotNull(result.SqlAtoms);
        Assert.NotNull(result.Links);
    }

    [Fact]
    public void ScannerPath_Can_Be_Set()
    {
        var scanner = new TypeScriptScanner { ScannerPath = "/custom/path" };
        Assert.Equal("/custom/path", scanner.ScannerPath);
    }

    [Fact]
    public async Task ScanAsync_Scans_TypeScript_Monorepo()
    {
        // Integration test: scan the digital_backbone if it exists
        var digitalBackbone = "/Users/baxter/devProject/digital_backbone";
        if (!Directory.Exists(digitalBackbone))
        {
            // Skip if digital_backbone not available
            return;
        }

        var scannerDir = FindScannerDirectory();
        if (scannerDir == null)
        {
            // Skip if scanner-typescript not built
            return;
        }

        var scanner = new TypeScriptScanner { ScannerPath = scannerDir };
        var result = await scanner.ScanAsync(digitalBackbone);

        // Should produce atoms and links
        Assert.NotEmpty(result.CodeAtoms);
        Assert.NotEmpty(result.Links);

        // Should contain TypeScript-specific atom types
        Assert.Contains(result.CodeAtoms, a => a.Type == AtomType.TypeAlias);
        Assert.Contains(result.CodeAtoms, a => a.Type == AtomType.Interface);
        Assert.Contains(result.CodeAtoms, a => a.Type == AtomType.Method);

        // Should contain TypeScript-specific link types
        Assert.Contains(result.Links, l => l.Type == LinkType.Imports);

        // All atoms should be TypeScript language
        Assert.All(result.CodeAtoms, a => Assert.Equal("TypeScript", a.Language));

        // Should have reasonable counts (from previous scan: ~3993 atoms, ~1457 links)
        Assert.True(result.CodeAtoms.Count > 100, 
            $"Expected > 100 atoms but got {result.CodeAtoms.Count}");
        Assert.True(result.Links.Count > 50, 
            $"Expected > 50 links but got {result.Links.Count}");
    }

    [Fact]
    public void AtomType_Enum_Supports_TypeScript_Values()
    {
        // Verify the new enum values exist
        Assert.Equal(AtomType.TypeAlias, Enum.Parse<AtomType>("TypeAlias"));
        Assert.Equal(AtomType.Module, Enum.Parse<AtomType>("Module"));
        Assert.Equal(AtomType.Component, Enum.Parse<AtomType>("Component"));
    }

    [Fact]
    public void LinkType_Enum_Supports_TypeScript_Values()
    {
        Assert.Equal(LinkType.Imports, Enum.Parse<LinkType>("Imports"));
        Assert.Equal(LinkType.ReExports, Enum.Parse<LinkType>("ReExports"));
        Assert.Equal(LinkType.WorkspaceDependency, Enum.Parse<LinkType>("WorkspaceDependency"));
    }

    [Fact]
    public void SnapshotMetadata_Has_TypeScript_Counters()
    {
        var metadata = new SnapshotMetadata
        {
            TypeAliasCount = 42,
            ModuleCount = 10,
            ComponentCount = 15
        };

        Assert.Equal(42, metadata.TypeAliasCount);
        Assert.Equal(10, metadata.ModuleCount);
        Assert.Equal(15, metadata.ComponentCount);
    }

    private static string? FindScannerDirectory()
    {
        // Look for scanner-typescript in the solution root
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "scanner-typescript");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "package.json")))
                return Path.GetFullPath(candidate);

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }
}
