using System.Text.RegularExpressions;
using Humanizer;
using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Linker;

/// <summary>
/// Semantic linker that connects C# code atoms to SQL atoms through multiple strategies.
/// </summary>
public partial class SemanticLinker
{
    private static readonly HashSet<string> StrippableSuffixes = 
        ["DTO", "Dto", "Entity", "Model", "Request", "Response", "ViewModel", "View"];

    /// <summary>
    /// Link code atoms to SQL atoms using multiple matching strategies.
    /// Also processes existing links for attribute bindings.
    /// </summary>
    public LinkResult LinkAtoms(
        IEnumerable<CodeAtom> codeAtoms, 
        IEnumerable<SqlAtom> sqlAtoms,
        IEnumerable<AtomLink>? existingLinks = null)
    {
        var result = new LinkResult();
        var codeList = codeAtoms.ToList();
        var sqlList = sqlAtoms.ToList();
        var existingLinkList = existingLinks?.ToList() ?? [];

        // Build lookup tables
        var tables = sqlList.Where(a => a.Type == SqlAtomType.Table)
            .DistinctBy(t => t.Name.ToLowerInvariant())
            .ToDictionary(t => t.Name.ToLowerInvariant(), t => t);
        var columns = sqlList.Where(a => a.Type == SqlAtomType.Column).ToList();
        var codeById = codeList.DistinctBy(c => c.Id).ToDictionary(c => c.Id, c => c);

        // Process attribute bindings from existing links (from scanner)
        var attributeLinkedAtoms = ProcessAttributeBindings(existingLinkList, tables, result);

        // Link each code atom
        foreach (var codeAtom in codeList)
        {
            // Skip if already linked via attribute
            if (attributeLinkedAtoms.Contains(codeAtom.Id))
                continue;

            switch (codeAtom.Type)
            {
                case AtomType.Class:
                case AtomType.Dto:
                case AtomType.Record:
                    LinkClassToTable(codeAtom, tables, result);
                    break;
                case AtomType.Property:
                    LinkPropertyToColumn(codeAtom, columns, existingLinkList, result);
                    break;
                case AtomType.Interface:
                    LinkInterfaceMethods(codeAtom, codeList, result);
                    break;
            }
        }

        // Process query traces from Dapper diagnostics
        ProcessQueryTraces(existingLinkList, tables, result);

        return result;
    }

    private HashSet<string> ProcessAttributeBindings(List<AtomLink> existingLinks, Dictionary<string, SqlAtom> tables, LinkResult result)
    {
        var linkedAtoms = new HashSet<string>();

        // Find links with AttributeBinding type (created by scanner for [Table] attributes)
        foreach (var link in existingLinks.Where(l => l.Type == LinkType.AttributeBinding))
        {
            // The target might be in format "table:TableName" - extract and match
            var targetName = link.TargetId.Replace("table:", "").ToLowerInvariant();
            
            if (tables.TryGetValue(targetName, out var table))
            {
                result.Links.Add(new AtomLink
                {
                    Id = $"attr-link-{link.SourceId}-{table.Id}",
                    SourceId = link.SourceId,
                    TargetId = table.Id,
                    Type = LinkType.AttributeBinding,
                    Confidence = 1.0,
                    Evidence = link.Evidence ?? "[Table] attribute binding"
                });
                linkedAtoms.Add(link.SourceId);
            }
        }

        return linkedAtoms;
    }

    private void LinkPropertyToColumn(CodeAtom property, List<SqlAtom> columns, List<AtomLink> existingLinks, LinkResult result)
    {
        // First check for [Column] attribute binding from scanner
        var columnAttrLink = existingLinks.FirstOrDefault(l => 
            l.SourceId == property.Id && l.Type == LinkType.AttributeBinding);
        
        if (columnAttrLink != null)
        {
            // Use the attribute binding with 1.0 confidence
            var targetColumnName = columnAttrLink.TargetId.Replace("column:", "").ToLowerInvariant();
            var matchedColumn = columns.FirstOrDefault(c => c.Name.ToLowerInvariant() == targetColumnName);
            
            if (matchedColumn != null)
            {
                result.Links.Add(new AtomLink
                {
                    Id = $"attr-link-{property.Id}-{matchedColumn.Id}",
                    SourceId = property.Id,
                    TargetId = matchedColumn.Id,
                    Type = LinkType.AttributeBinding,
                    Confidence = 1.0,
                    Evidence = columnAttrLink.Evidence ?? "[Column] attribute binding"
                });
                return;
            }
        }

        // Fall back to name-based matching
        var normalizedName = property.Name.ToLowerInvariant();
        foreach (var column in columns)
        {
            if (column.Name.ToLowerInvariant() == normalizedName)
            {
                result.Links.Add(new AtomLink
                {
                    Id = $"link-{property.Id}-{column.Id}",
                    SourceId = property.Id,
                    TargetId = column.Id,
                    Type = LinkType.PropertyMatch,
                    Confidence = 0.90,
                    Evidence = $"Property-column match: {property.Name} = {column.Name}"
                });
            }
        }
    }

    private void LinkInterfaceMethods(CodeAtom interfaceAtom, List<CodeAtom> allAtoms, LinkResult result)
    {
        // Find method atoms that belong to this interface
        var methods = allAtoms.Where(a => 
            a.Type == AtomType.Method && 
            a.Id.StartsWith(interfaceAtom.Id + "-", StringComparison.OrdinalIgnoreCase)).ToList();

        // Find DTOs and other types that might be referenced
        // Use GroupBy to handle duplicate names (same class name in different namespaces)
        var dtos = allAtoms.Where(a => 
            a.Type == AtomType.Dto || a.Type == AtomType.Record || a.Type == AtomType.Class)
            .GroupBy(d => d.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var method in methods)
        {
            if (string.IsNullOrEmpty(method.Signature)) continue;

            // Extract return type from signature (e.g., "Task<UserDTO> GetByIdAsync(int id)")
            var returnTypeMatch = ReturnTypeRegex().Match(method.Signature);
            if (returnTypeMatch.Success)
            {
                var returnType = returnTypeMatch.Groups[1].Value;
                // Handle Task<T>, IEnumerable<T>, etc.
                var genericMatch = GenericTypeRegex().Match(returnType);
                var actualType = genericMatch.Success ? genericMatch.Groups[1].Value : returnType;

                if (dtos.TryGetValue(actualType.ToLowerInvariant(), out var returnDto))
                {
                    result.Links.Add(new AtomLink
                    {
                        Id = $"returns-{method.Id}-{returnDto.Id}",
                        SourceId = method.Id,
                        TargetId = returnDto.Id,
                        Type = LinkType.References,
                        Confidence = 1.0,
                        Evidence = $"Method returns {returnDto.Name}"
                    });
                }
            }

            // Extract parameter types
            var paramMatch = ParameterTypesRegex().Match(method.Signature);
            if (paramMatch.Success)
            {
                var paramsStr = paramMatch.Groups[1].Value;
                var paramTypes = paramsStr.Split(',')
                    .Select(p => p.Trim().Split(' ').FirstOrDefault())
                    .Where(t => !string.IsNullOrEmpty(t));

                foreach (var paramType in paramTypes)
                {
                    if (dtos.TryGetValue(paramType!.ToLowerInvariant(), out var paramDto))
                    {
                        result.Links.Add(new AtomLink
                        {
                            Id = $"param-{method.Id}-{paramDto.Id}",
                            SourceId = method.Id,
                            TargetId = paramDto.Id,
                            Type = LinkType.References,
                            Confidence = 1.0,
                            Evidence = $"Method takes {paramDto.Name} as parameter"
                        });
                    }
                }
            }
        }
    }

    private void ProcessQueryTraces(List<AtomLink> existingLinks, Dictionary<string, SqlAtom> tables, LinkResult result)
    {
        // Process links with QueryTrace type (from Dapper detection)
        foreach (var link in existingLinks.Where(l => l.Type == LinkType.QueryTrace))
        {
            var tableName = link.TargetId.Replace("table:", "").ToLowerInvariant();
            if (tables.TryGetValue(tableName, out var table))
            {
                result.Links.Add(new AtomLink
                {
                    Id = $"query-{link.SourceId}-{table.Id}",
                    SourceId = link.SourceId,
                    TargetId = table.Id,
                    Type = LinkType.QueryTrace,
                    Confidence = 0.85,
                    Evidence = link.Evidence ?? "SQL query trace"
                });
            }
        }
    }

    /// <summary>
    /// Parse SQL query and extract table references.
    /// </summary>
    public List<string> ExtractTablesFromSql(string sql)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Match FROM clause
        var fromMatches = FromClauseRegex().Matches(sql);
        foreach (Match match in fromMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // Match JOIN clauses
        var joinMatches = JoinClauseRegex().Matches(sql);
        foreach (Match match in joinMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // Match INSERT INTO
        var insertMatches = InsertClauseRegex().Matches(sql);
        foreach (Match match in insertMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // Match UPDATE
        var updateMatches = UpdateClauseRegex().Matches(sql);
        foreach (Match match in updateMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // Match DELETE FROM
        var deleteMatches = DeleteClauseRegex().Matches(sql);
        foreach (Match match in deleteMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        return [.. tables];
    }

    private void LinkClassToTable(CodeAtom codeAtom, Dictionary<string, SqlAtom> tables, LinkResult result)
    {
        // Strategy 1: Exact name match (attribute binding handled separately)
        var exactMatch = TryExactNameMatch(codeAtom, tables);
        if (exactMatch != null)
        {
            result.Links.Add(exactMatch);
            return;
        }

        // Strategy 2: Fuzzy match (suffix stripping, pluralization)
        var fuzzyMatch = TryFuzzyNameMatch(codeAtom, tables);
        if (fuzzyMatch != null)
        {
            result.Links.Add(fuzzyMatch);
        }
    }

    private AtomLink? TryExactNameMatch(CodeAtom codeAtom, Dictionary<string, SqlAtom> tables)
    {
        var normalizedName = codeAtom.Name.ToLowerInvariant();
        
        if (tables.TryGetValue(normalizedName, out var table))
        {
            return new AtomLink
            {
                Id = $"link-{codeAtom.Id}-{table.Id}",
                SourceId = codeAtom.Id,
                TargetId = table.Id,
                Type = LinkType.ExactMatch,
                Confidence = 0.95,
                Evidence = $"Exact name match: {codeAtom.Name} = {table.Name}"
            };
        }

        return null;
    }

    private AtomLink? TryFuzzyNameMatch(CodeAtom codeAtom, Dictionary<string, SqlAtom> tables)
    {
        var baseName = StripKnownSuffixes(codeAtom.Name);
        var normalizedBase = baseName.ToLowerInvariant();

        // Try pluralized version
        var pluralized = baseName.Pluralize().ToLowerInvariant();
        if (tables.TryGetValue(pluralized, out var pluralTable))
        {
            return new AtomLink
            {
                Id = $"link-{codeAtom.Id}-{pluralTable.Id}",
                SourceId = codeAtom.Id,
                TargetId = pluralTable.Id,
                Type = LinkType.FuzzyMatch,
                Confidence = 0.85,
                Evidence = $"Pluralization match: {codeAtom.Name} → {pluralTable.Name}"
            };
        }

        // Try singular version (table might be singular)
        var singularized = baseName.Singularize().ToLowerInvariant();
        if (tables.TryGetValue(singularized, out var singularTable))
        {
            return new AtomLink
            {
                Id = $"link-{codeAtom.Id}-{singularTable.Id}",
                SourceId = codeAtom.Id,
                TargetId = singularTable.Id,
                Type = LinkType.FuzzyMatch,
                Confidence = 0.80,
                Evidence = $"Singularization match: {codeAtom.Name} → {singularTable.Name}"
            };
        }

        // Try base name after suffix stripping (if different from original)
        if (normalizedBase != codeAtom.Name.ToLowerInvariant() && tables.TryGetValue(normalizedBase, out var baseTable))
        {
            return new AtomLink
            {
                Id = $"link-{codeAtom.Id}-{baseTable.Id}",
                SourceId = codeAtom.Id,
                TargetId = baseTable.Id,
                Type = LinkType.FuzzyMatch,
                Confidence = 0.80,
                Evidence = $"Suffix stripping: {codeAtom.Name} → {baseTable.Name}"
            };
        }

        // Try pluralized base name
        var pluralizedBase = baseName.Pluralize().ToLowerInvariant();
        if (normalizedBase != codeAtom.Name.ToLowerInvariant() && tables.TryGetValue(pluralizedBase, out var pluralBaseTable))
        {
            return new AtomLink
            {
                Id = $"link-{codeAtom.Id}-{pluralBaseTable.Id}",
                SourceId = codeAtom.Id,
                TargetId = pluralBaseTable.Id,
                Type = LinkType.FuzzyMatch,
                Confidence = 0.80,
                Evidence = $"Suffix strip + pluralize: {codeAtom.Name} → {pluralBaseTable.Name}"
            };
        }

        return null;
    }

    private string StripKnownSuffixes(string name)
    {
        foreach (var suffix in StrippableSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
            {
                return name[..^suffix.Length];
            }
        }
        return name;
    }

    /// <summary>
    /// Get all atoms that depend on the specified atom (consumers).
    /// </summary>
    public List<string> GetDependents(string atomId, IEnumerable<AtomLink> allLinks)
    {
        return allLinks
            .Where(l => l.TargetId == atomId)
            .Select(l => l.SourceId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Get all atoms that the specified atom depends on (dependencies).
    /// </summary>
    public List<string> GetDependencies(string atomId, IEnumerable<AtomLink> allLinks)
    {
        return allLinks
            .Where(l => l.SourceId == atomId)
            .Select(l => l.TargetId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Calculate the blast radius - all transitively affected atoms up to maxDepth.
    /// </summary>
    public BlastRadiusResult GetBlastRadius(string atomId, IEnumerable<AtomLink> allLinks, int maxDepth = 5)
    {
        var result = new BlastRadiusResult { RootAtomId = atomId };
        var visited = new HashSet<string> { atomId };
        var currentLevel = new List<string> { atomId };
        var linkList = allLinks.ToList();

        for (int depth = 1; depth <= maxDepth && currentLevel.Count > 0; depth++)
        {
            var nextLevel = new List<string>();
            
            foreach (var id in currentLevel)
            {
                var dependents = GetDependents(id, linkList);
                foreach (var dependent in dependents)
                {
                    if (visited.Add(dependent))
                    {
                        nextLevel.Add(dependent);
                        result.AffectedAtoms.Add(new AffectedAtom
                        {
                            AtomId = dependent,
                            Depth = depth,
                            Path = BuildPath(atomId, dependent, linkList)
                        });
                    }
                }
            }

            currentLevel = nextLevel;
        }

        return result;
    }

    private string BuildPath(string from, string to, List<AtomLink> links)
    {
        var link = links.FirstOrDefault(l => l.TargetId == from && l.SourceId == to);
        return link != null ? $"{to} → {from}" : $"{to} → ... → {from}";
    }

    // Regex patterns for SQL parsing
    [GeneratedRegex(@"\bFROM\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex FromClauseRegex();

    [GeneratedRegex(@"\bJOIN\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex JoinClauseRegex();

    [GeneratedRegex(@"\bINSERT\s+INTO\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex InsertClauseRegex();

    [GeneratedRegex(@"\bUPDATE\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex UpdateClauseRegex();

    [GeneratedRegex(@"\bDELETE\s+FROM\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeleteClauseRegex();

    // Regex patterns for method signature parsing
    [GeneratedRegex(@"^(\S+)\s+\w+\s*\(")]
    private static partial Regex ReturnTypeRegex();

    [GeneratedRegex(@"<(\w+)>")]
    private static partial Regex GenericTypeRegex();

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex ParameterTypesRegex();
}

/// <summary>
/// Result of a linking operation.
/// </summary>
public class LinkResult
{
    public List<AtomLink> Links { get; } = [];
    public List<string> Diagnostics { get; } = [];
}

/// <summary>
/// Result of a blast radius calculation.
/// </summary>
public class BlastRadiusResult
{
    public required string RootAtomId { get; init; }
    public List<AffectedAtom> AffectedAtoms { get; } = [];
    public int TotalAffected => AffectedAtoms.Count;
    public int MaxDepth => AffectedAtoms.Count > 0 ? AffectedAtoms.Max(a => a.Depth) : 0;
}

/// <summary>
/// An atom affected by a change.
/// </summary>
public class AffectedAtom
{
    public required string AtomId { get; init; }
    public required int Depth { get; init; }
    public required string Path { get; init; }
}
