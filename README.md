# Mystery Machine

> Atomic-level codebase intelligence for enterprise legacy modernization

Mystery Machine scans .NET and SQL codebases to extract **atomic elements** (DTOs, interfaces, tables, stored procedures), **link them semantically**, and provide **risk scoring** for safe evolution.

## Quick Start

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run the CLI
dotnet run --project src/SystemCartographer.Cli -- --help
```

## CLI Commands

```bash
# Scan a repository
cartographer scan --repo ./src --output ./snapshot.json

# Compare snapshots for breaking changes
cartographer diff --baseline main.json --snapshot current.json

# Update central database (post-merge)
cartographer update --snapshot ./snapshot.json --connection "Server=..."
```

## Project Structure

```
src/
├── SystemCartographer.Core           # Atomic model types, interfaces
├── SystemCartographer.Scanner.CSharp # Roslyn-based C# scanner
├── SystemCartographer.Scanner.Sql    # ScriptDOM T-SQL scanner
├── SystemCartographer.Linker         # Semantic linking engine
├── SystemCartographer.Risk           # Risk scoring calculator
├── SystemCartographer.Federation     # Multi-repo merge engine
└── SystemCartographer.Cli            # Command-line tool
tests/
└── SystemCartographer.Tests          # Unit tests
```

## Target Framework

The solution currently targets **net10.0** for local development.

To deploy for .NET 8 environments:

1. Edit `Directory.Build.props`
2. Change `<TargetFramework>net10.0</TargetFramework>` to `<TargetFramework>net8.0</TargetFramework>`
3. Rebuild: `dotnet build`

## Dependencies

| Package                                   | Purpose                       |
| ----------------------------------------- | ----------------------------- |
| Microsoft.CodeAnalysis.CSharp             | Roslyn AST for C# parsing     |
| Microsoft.SqlServer.TransactSql.ScriptDom | T-SQL parsing                 |
| Humanizer.Core                            | Name matching (pluralization) |
| Microsoft.Data.SqlClient                  | SQL Server connectivity       |
| xUnit                                     | Unit testing                  |


