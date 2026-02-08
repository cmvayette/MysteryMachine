using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Scanner.CSharp;

/// <summary>
/// Scans C# source code using Roslyn to extract atomic elements.
/// </summary>
public class CSharpScanner : IScanner
{
    private static readonly HashSet<string> DtoSuffixes = ["DTO", "Dto", "Request", "Response", "ViewModel", "Model"];
    private static readonly HashSet<string> DtoAttributes = ["DataContract", "DataContractAttribute", "Serializable", "SerializableAttribute"];
    
    public async Task<ScanResult> ScanAsync(string path, ScanOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var startTime = DateTime.UtcNow;
        var result = new ScanResult
        {
            CodeAtoms = [],
            SqlAtoms = [],
            Links = [],
            Diagnostics = []
        };

        // Determine if path is a file or directory
        if (File.Exists(path))
        {
            await ScanFileAsync(path, result, options, cancellationToken);
        }
        else if (Directory.Exists(path))
        {
            await ScanDirectoryAsync(path, result, options, cancellationToken);
        }
        else
        {
            result.Diagnostics.Add(new ScanDiagnostic(Core.DiagnosticSeverity.Error, $"Path not found: {path}"));
        }

        return result with { Duration = DateTime.UtcNow - startTime };
    }

    /// <summary>
    /// Scan a single C# source string (for testing).
    /// </summary>
    public ScanResult ScanSource(string sourceCode, string fileName = "source.cs")
    {
        var result = new ScanResult
        {
            CodeAtoms = [],
            SqlAtoms = [],
            Links = [],
            Diagnostics = []
        };

        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: fileName);
        var root = tree.GetCompilationUnitRoot();
        
        ExtractAtomsFromSyntaxTree(root, fileName, result);
        
        return result;
    }

    private async Task ScanDirectoryAsync(string directory, ScanResult result, ScanOptions options, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f, options.ExcludePatterns));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanFileAsync(file, result, options, cancellationToken);
        }
    }

    private async Task ScanFileAsync(string filePath, ScanResult result, ScanOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            var tree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath);
            var root = tree.GetCompilationUnitRoot();

            // Check for parse errors
            var errors = tree.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            foreach (var error in errors)
            {
                result.Diagnostics.Add(new ScanDiagnostic(
                    Core.DiagnosticSeverity.Error,
                    error.GetMessage(),
                    filePath,
                    error.Location.GetLineSpan().StartLinePosition.Line + 1
                ));
            }

            ExtractAtomsFromSyntaxTree(root, filePath, result, options.IncludePrivateMembers);
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new ScanDiagnostic(
                Core.DiagnosticSeverity.Error,
                $"Failed to scan file: {ex.Message}",
                filePath
            ));
        }
    }

    private void ExtractAtomsFromSyntaxTree(CompilationUnitSyntax root, string filePath, ScanResult result, bool includePrivate = false)
    {
        // Extract namespace context
        var namespaceVisitor = new NamespaceVisitor();
        namespaceVisitor.Visit(root);

        // Extract types
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var atom = ExtractTypeAtom(typeDecl, filePath, includePrivate);
            if (atom != null)
            {
                result.CodeAtoms.Add(atom);
                
                // Extract members (properties, methods)
                ExtractMembers(typeDecl, atom.Id, filePath, result, includePrivate);
            }
        }

        // Extract enums
        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            var atom = ExtractEnumAtom(enumDecl, filePath);
            if (atom != null)
            {
                result.CodeAtoms.Add(atom);
            }
        }

        // Detect Dapper inline SQL
        ExtractDapperQueries(root, filePath, result);
    }

    private CodeAtom? ExtractEnumAtom(EnumDeclarationSyntax enumDecl, string filePath)
    {
        if (!IsPublic(enumDecl))
            return null;

        var name = enumDecl.Identifier.Text;
        var ns = GetNamespace(enumDecl);
        var lineSpan = enumDecl.GetLocation().GetLineSpan();
        var startLine = lineSpan.StartLinePosition.Line + 1;
        var endLine = lineSpan.EndLinePosition.Line + 1;
        var linesOfCode = endLine - startLine + 1;

        var members = enumDecl.Members.Select(m => m.Identifier.Text);
        var signature = $"enum {name} {{ {string.Join(", ", members)} }}";

        return new CodeAtom
        {
            Id = $"{ns}.{name}".ToLowerInvariant().Replace(".", "-"),
            Name = name,
            Type = AtomType.Enum,
            Namespace = ns,
            FilePath = filePath,
            LineNumber = startLine,
            LinesOfCode = linesOfCode,
            Language = "csharp",
            Signature = signature,
            IsPublic = true // Checked by guard clause
        };
    }

    private bool IsPublic(EnumDeclarationSyntax enumDecl)
    {
        return enumDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private CodeAtom? ExtractTypeAtom(TypeDeclarationSyntax typeDecl, string filePath, bool includePrivate)
    {
        // Skip non-public unless configured
        if (!includePrivate && !IsPublic(typeDecl))
            return null;

        var name = typeDecl.Identifier.Text;
        var ns = GetNamespace(typeDecl);
        var atomType = ClassifyType(typeDecl);
        var lineSpan = typeDecl.GetLocation().GetLineSpan();
        var startLine = lineSpan.StartLinePosition.Line + 1;
        var endLine = lineSpan.EndLinePosition.Line + 1;
        var linesOfCode = endLine - startLine + 1;

        // Build signature
        var signature = BuildTypeSignature(typeDecl);

        return new CodeAtom
        {
            Id = $"{ns}.{name}".ToLowerInvariant().Replace(".", "-"),
            Name = name,
            Type = atomType,
            Namespace = ns,
            FilePath = filePath,
            LineNumber = startLine,
            LinesOfCode = linesOfCode,
            Language = "csharp",
            Signature = signature,
            IsPublic = IsPublic(typeDecl)
        };
    }

    private AtomType ClassifyType(TypeDeclarationSyntax typeDecl)
    {
        var name = typeDecl.Identifier.Text;

        // Check explicit type
        if (typeDecl is InterfaceDeclarationSyntax)
            return AtomType.Interface;
        if (typeDecl is RecordDeclarationSyntax)
            return AtomType.Record;
        if (typeDecl is StructDeclarationSyntax)
            return AtomType.Struct;

        // Check for DTO patterns
        if (IsDtoByNaming(name) || IsDtoByAttribute(typeDecl))
            return AtomType.Dto;

        return AtomType.Class;
    }

    private bool IsDtoByNaming(string name)
    {
        return DtoSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsDtoByAttribute(TypeDeclarationSyntax typeDecl)
    {
        var attributes = typeDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Select(a => a.Name.ToString());
        
        return attributes.Any(a => DtoAttributes.Contains(a));
    }

    private void ExtractMembers(TypeDeclarationSyntax typeDecl, string parentId, string filePath, ScanResult result, bool includePrivate)
    {
        // Extract properties
        foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (!includePrivate && !IsPublicMember(prop.Modifiers))
                continue;

            var propAtom = new CodeAtom
            {
                Id = $"{parentId}-{prop.Identifier.Text}".ToLowerInvariant(),
                Name = prop.Identifier.Text,
                Type = AtomType.Property,
                Namespace = GetNamespace(typeDecl),
                FilePath = filePath,
                LineNumber = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Language = "csharp",
                Signature = $"{prop.Type} {prop.Identifier.Text}"
            };
            result.CodeAtoms.Add(propAtom);

            // Create containment link
            result.Links.Add(new AtomLink
            {
                Id = $"contains-{parentId}-{propAtom.Id}",
                SourceId = parentId,
                TargetId = propAtom.Id,
                Type = LinkType.Contains,
                Confidence = 1.0
            });

            // Extract EF Core attributes
            ExtractPropertyAttributes(prop, propAtom, result);
        }

        // Extract interface methods
        if (typeDecl is InterfaceDeclarationSyntax)
        {
            foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodAtom = new CodeAtom
                {
                    Id = $"{parentId}-{method.Identifier.Text}".ToLowerInvariant(),
                    Name = method.Identifier.Text,
                    Type = AtomType.Method,
                    Namespace = GetNamespace(typeDecl),
                    FilePath = filePath,
                    LineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Language = "csharp",
                    Signature = BuildMethodSignature(method)
                };
                result.CodeAtoms.Add(methodAtom);

                result.Links.Add(new AtomLink
                {
                    Id = $"contains-{parentId}-{methodAtom.Id}",
                    SourceId = parentId,
                    TargetId = methodAtom.Id,
                    Type = LinkType.Contains,
                    Confidence = 1.0
                });
            }
        }
    }

    private void ExtractPropertyAttributes(PropertyDeclarationSyntax prop, CodeAtom propAtom, ScanResult result)
    {
        foreach (var attrList in prop.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                
                // [Column("ColumnName")] - extract the column mapping
                if (attrName is "Column" or "ColumnAttribute")
                {
                    var columnName = ExtractAttributeArgument(attr);
                    if (columnName != null)
                    {
                        result.Links.Add(new AtomLink
                        {
                            Id = $"ef-column-{propAtom.Id}",
                            SourceId = propAtom.Id,
                            TargetId = $"column:{columnName}".ToLowerInvariant(),
                            Type = LinkType.AttributeBinding,
                            Confidence = 1.0,
                            Evidence = $"[Column(\"{columnName}\")] attribute"
                        });
                    }
                }
            }
        }
    }

    private void ExtractDapperQueries(CompilationUnitSyntax root, string filePath, ScanResult result)
    {
        var dapperMethods = new[] { "Query", "QueryAsync", "QueryFirst", "QueryFirstAsync", 
            "QueryFirstOrDefault", "QueryFirstOrDefaultAsync", "Execute", "ExecuteAsync" };

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                _ => null
            };

            if (methodName != null && dapperMethods.Contains(methodName))
            {
                // Extract SQL string from first argument
                var firstArg = invocation.ArgumentList.Arguments.FirstOrDefault();
                if (firstArg?.Expression is LiteralExpressionSyntax literal)
                {
                    var sql = literal.Token.ValueText;
                    // Create diagnostic noting the inline SQL
                    result.Diagnostics.Add(new ScanDiagnostic(
                        Core.DiagnosticSeverity.Info,
                        $"Dapper SQL detected: {TruncateSql(sql)}",
                        filePath,
                        invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                    ));
                }
            }
        }
    }

    private static string TruncateSql(string sql) 
        => sql.Length > 80 ? sql[..77] + "..." : sql;

    private string? ExtractAttributeArgument(AttributeSyntax attr)
    {
        var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
        if (arg?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }
        return null;
    }

    private string BuildTypeSignature(TypeDeclarationSyntax typeDecl)
    {
        var modifiers = string.Join(" ", typeDecl.Modifiers.Select(m => m.Text));
        var keyword = typeDecl.Keyword.Text;
        var name = typeDecl.Identifier.Text;
        
        var baseList = typeDecl.BaseList != null 
            ? $" : {string.Join(", ", typeDecl.BaseList.Types.Select(t => t.ToString()))}"
            : "";

        return $"{modifiers} {keyword} {name}{baseList}".Trim();
    }

    private string BuildMethodSignature(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType.ToString();
        var name = method.Identifier.Text;
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
        return $"{returnType} {name}({parameters})";
    }

    private string GetNamespace(SyntaxNode node)
    {
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return ns?.Name.ToString() ?? "global";
    }

    private bool IsPublic(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private bool IsPublicMember(SyntaxTokenList modifiers)
    {
        return modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private bool IsExcluded(string path, IEnumerable<string> excludePatterns)
    {
        return excludePatterns.Any(pattern => 
            path.Contains(pattern.Replace("**", "").Replace("*", ""), StringComparison.OrdinalIgnoreCase));
    }

    private class NamespaceVisitor : CSharpSyntaxWalker
    {
        public List<string> Namespaces { get; } = [];

        public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            Namespaces.Add(node.Name.ToString());
            base.VisitFileScopedNamespaceDeclaration(node);
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            Namespaces.Add(node.Name.ToString());
            base.VisitNamespaceDeclaration(node);
        }
    }
}
