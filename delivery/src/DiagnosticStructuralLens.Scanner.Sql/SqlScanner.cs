using Microsoft.SqlServer.TransactSql.ScriptDom;
using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Scanner.Sql;

/// <summary>
/// Scans T-SQL source using ScriptDOM to extract atomic elements.
/// </summary>
public class SqlScanner : IScanner
{
    private readonly TSql160Parser _parser = new(initialQuotedIdentifiers: true);

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

        if (File.Exists(path))
        {
            await ScanFileAsync(path, result, cancellationToken);
        }
        else if (Directory.Exists(path))
        {
            await ScanDirectoryAsync(path, result, options, cancellationToken);
        }
        else
        {
            result.Diagnostics.Add(new ScanDiagnostic(DiagnosticSeverity.Error, $"Path not found: {path}"));
        }

        return result with { Duration = DateTime.UtcNow - startTime };
    }

    /// <summary>
    /// Scan a single SQL source string (for testing).
    /// </summary>
    public ScanResult ScanSource(string sqlSource)
    {
        var result = new ScanResult
        {
            CodeAtoms = [],
            SqlAtoms = [],
            Links = [],
            Diagnostics = []
        };

        ParseAndExtract(sqlSource, "source.sql", result);
        return result;
    }

    private async Task ScanDirectoryAsync(string directory, ScanResult result, ScanOptions options, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f, options.ExcludePatterns));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanFileAsync(file, result, cancellationToken);
        }
    }

    private async Task ScanFileAsync(string filePath, ScanResult result, CancellationToken cancellationToken)
    {
        try
        {
            var sql = await File.ReadAllTextAsync(filePath, cancellationToken);
            ParseAndExtract(sql, filePath, result);
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new ScanDiagnostic(
                DiagnosticSeverity.Error,
                $"Failed to scan file: {ex.Message}",
                filePath
            ));
        }
    }

    private void ParseAndExtract(string sql, string filePath, ScanResult result)
    {
        using var reader = new StringReader(sql);
        var fragment = _parser.Parse(reader, out var errors);

        foreach (var error in errors)
        {
            result.Diagnostics.Add(new ScanDiagnostic(
                DiagnosticSeverity.Error,
                error.Message,
                filePath,
                error.Line
            ));
        }

        if (fragment is TSqlScript script)
        {
            foreach (var batch in script.Batches)
            {
                ExtractFromBatch(batch, filePath, result);
            }
        }
    }

    private void ExtractFromBatch(TSqlBatch batch, string filePath, ScanResult result)
    {
        foreach (var statement in batch.Statements)
        {
            switch (statement)
            {
                case CreateTableStatement createTable:
                    ExtractTable(createTable, filePath, result);
                    break;
                case CreateProcedureStatement createProc:
                    ExtractProcedure(createProc, filePath, result);
                    break;
                case CreateViewStatement createView:
                    ExtractView(createView, filePath, result);
                    break;
                case CreateFunctionStatement createFunc:
                    ExtractFunction(createFunc, filePath, result);
                    break;
            }
        }
    }

    private void ExtractTable(CreateTableStatement stmt, string filePath, ScanResult result)
    {
        var tableName = GetSchemaObjectName(stmt.SchemaObjectName);
        var tableId = $"table:{tableName}".ToLowerInvariant();

        result.SqlAtoms.Add(new SqlAtom
        {
            Id = tableId,
            Name = tableName,
            Type = SqlAtomType.Table,
            FilePath = filePath
        });

        // Extract columns
        foreach (var column in stmt.Definition.ColumnDefinitions)
        {
            var columnName = column.ColumnIdentifier.Value;
            var dataType = column.DataType?.Name?.BaseIdentifier?.Value ?? "unknown";
            var isNullable = !column.Constraints.Any(c => c is NullableConstraintDefinition { Nullable: false });
            var isIdentity = column.IdentityOptions != null;
            var isPrimaryKey = column.Constraints.Any(c => c is UniqueConstraintDefinition { IsPrimaryKey: true });

            var columnId = $"column:{tableName}.{columnName}".ToLowerInvariant();

            result.SqlAtoms.Add(new SqlAtom
            {
                Id = columnId,
                Name = columnName,
                Type = SqlAtomType.Column,
                ParentTable = tableName,
                DataType = BuildDataTypeString(column.DataType),
                IsNullable = isNullable,
                FilePath = filePath
            });

            // Link column to table
            result.Links.Add(new AtomLink
            {
                Id = $"contains-{tableId}-{columnId}",
                SourceId = tableId,
                TargetId = columnId,
                Type = LinkType.Contains,
                Confidence = 1.0
            });
        }

        // Extract foreign keys
        foreach (var constraint in stmt.Definition.TableConstraints.OfType<ForeignKeyConstraintDefinition>())
        {
            var referencedTable = GetSchemaObjectName(constraint.ReferenceTableName);
            result.Links.Add(new AtomLink
            {
                Id = $"fk-{tableName}-{referencedTable}".ToLowerInvariant(),
                SourceId = tableId,
                TargetId = $"table:{referencedTable}".ToLowerInvariant(),
                Type = LinkType.References,
                Confidence = 1.0,
                Evidence = "Foreign key constraint"
            });
        }
    }

    private void ExtractProcedure(CreateProcedureStatement stmt, string filePath, ScanResult result)
    {
        var procName = GetSchemaObjectName(stmt.ProcedureReference.Name);
        var procId = $"proc:{procName}".ToLowerInvariant();

        result.SqlAtoms.Add(new SqlAtom
        {
            Id = procId,
            Name = procName,
            Type = SqlAtomType.StoredProcedure,
            FilePath = filePath
        });

        // Analyze for CRUD operations
        var visitor = new CrudVisitor();
        stmt.Accept(visitor);

        foreach (var table in visitor.SelectTables)
        {
            result.Links.Add(new AtomLink
            {
                Id = $"reads-{procId}-{table}".ToLowerInvariant(),
                SourceId = procId,
                TargetId = $"table:{table}".ToLowerInvariant(),
                Type = LinkType.References,
                Confidence = 0.9,
                Evidence = "SELECT statement"
            });
        }

        foreach (var table in visitor.WriteTables)
        {
            result.Links.Add(new AtomLink
            {
                Id = $"writes-{procId}-{table}".ToLowerInvariant(),
                SourceId = procId,
                TargetId = $"table:{table}".ToLowerInvariant(),
                Type = LinkType.References,
                Confidence = 0.9,
                Evidence = "INSERT/UPDATE/DELETE statement"
            });
        }
    }

    private void ExtractView(CreateViewStatement stmt, string filePath, ScanResult result)
    {
        var viewName = GetSchemaObjectName(stmt.SchemaObjectName);
        var viewId = $"view:{viewName}".ToLowerInvariant();

        result.SqlAtoms.Add(new SqlAtom
        {
            Id = viewId,
            Name = viewName,
            Type = SqlAtomType.View,
            FilePath = filePath
        });

        // Find referenced tables
        var visitor = new CrudVisitor();
        stmt.Accept(visitor);

        foreach (var table in visitor.SelectTables)
        {
            result.Links.Add(new AtomLink
            {
                Id = $"depends-{viewId}-{table}".ToLowerInvariant(),
                SourceId = viewId,
                TargetId = $"table:{table}".ToLowerInvariant(),
                Type = LinkType.References,
                Confidence = 1.0,
                Evidence = "View dependency"
            });
        }
    }

    private void ExtractFunction(CreateFunctionStatement stmt, string filePath, ScanResult result)
    {
        var funcName = GetSchemaObjectName(stmt.Name);
        var funcId = $"func:{funcName}".ToLowerInvariant();

        result.SqlAtoms.Add(new SqlAtom
        {
            Id = funcId,
            Name = funcName,
            Type = SqlAtomType.Function,
            FilePath = filePath
        });
    }

    private string GetSchemaObjectName(SchemaObjectName name)
    {
        return name.BaseIdentifier.Value;
    }

    private string BuildDataTypeString(DataTypeReference? dataType)
    {
        if (dataType == null) return "unknown";
        
        var typeName = dataType.Name?.BaseIdentifier?.Value ?? "unknown";
        
        if (dataType is SqlDataTypeReference sqlType && sqlType.Parameters.Count > 0)
        {
            var parameters = string.Join(",", sqlType.Parameters.Select(p => p.Value));
            return $"{typeName}({parameters})";
        }
        
        return typeName;
    }

    private bool IsExcluded(string path, IEnumerable<string> excludePatterns)
    {
        return excludePatterns.Any(pattern =>
            path.Contains(pattern.Replace("**", "").Replace("*", ""), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Visitor to find tables referenced by SELECT, INSERT, UPDATE, DELETE.
    /// </summary>
    private class CrudVisitor : TSqlFragmentVisitor
    {
        public HashSet<string> SelectTables { get; } = [];
        public HashSet<string> WriteTables { get; } = [];

        public override void Visit(NamedTableReference node)
        {
            var tableName = node.SchemaObject.BaseIdentifier.Value;
            
            // Determine if this is a read or write based on parent
            var parent = GetStatementParent(node);
            if (parent is SelectStatement)
            {
                SelectTables.Add(tableName);
            }
            else if (parent is InsertStatement or UpdateStatement or DeleteStatement)
            {
                WriteTables.Add(tableName);
            }
            else
            {
                // Default to read for joins, subqueries, etc.
                SelectTables.Add(tableName);
            }
        }

        private TSqlStatement? GetStatementParent(TSqlFragment node)
        {
            // Walk up to find the statement type
            // Note: ScriptDOM doesn't have parent references, so we track context during traversal
            // This is a simplified implementation
            return null;
        }
    }
}
