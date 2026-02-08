using System.Text;

// args: [output_path] [project_count] [classes_per_project] [connectivity_0_1]
var outputPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "MassiveRepo");
var projectCount = args.Length > 1 ? int.Parse(args[1]) : 5;
var classesPerProject = args.Length > 2 ? int.Parse(args[2]) : 50;
var connectivity = args.Length > 3 ? double.Parse(args[3]) : 0.3;

Console.WriteLine($"🚀 Generating Synthetic Repo at: {outputPath}");
Console.WriteLine($"   Projects: {projectCount}");
Console.WriteLine($"   Classes/Project: {classesPerProject}");
Console.WriteLine($"   Total Classes: {projectCount * classesPerProject}");

if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
Directory.CreateDirectory(outputPath);

// Create Solution
await File.WriteAllTextAsync(Path.Combine(outputPath, "Massive.sln"), "");

var allClassNames = new List<string>();

// 1. Generate Structure
for (int i = 0; i < projectCount; i++)
{
    var projName = $"Project_{i}";
    var projPath = Path.Combine(outputPath, projName);
    Directory.CreateDirectory(projPath);
    
    // Create CSPROJ
    var csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
    await File.WriteAllTextAsync(Path.Combine(projPath, $"{projName}.csproj"), csproj);

    for (int j = 0; j < classesPerProject; j++)
    {
        var className = $"Class_{i}_{j}";
        allClassNames.Add($"{projName}.{className}");
        
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {projName};");
        sb.AppendLine();
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");
        sb.AppendLine($"    public string Id {{ get; set; }} = \"{Guid.NewGuid()}\";");
        sb.AppendLine();
        
        // Generate some methods
        for (int k = 0; k < 5; k++)
        {
            sb.AppendLine($"    public void Method_{k}()");
            sb.AppendLine("    {");
            sb.AppendLine($"        Console.WriteLine(\"Executing {className}.Method_{k}\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        await File.WriteAllTextAsync(Path.Combine(projPath, $"{className}.cs"), sb.ToString());
    }
}

// 2. Add Dependencies (Second Pass)
// Ideally we'd do this via csproj references but for a simplified graph test, 
// we'll primarily rely on file structure. However, to test cross-repo links, 
// we might need more sophistication. For now, let's keep projects independent 
// to avoid circular dependency hell in generation, but cross-link classes conceptually via comments or loose coupling if needed.
// Actually, let's add some cross-project references in the CSPROJ if connectivity is high.

Console.WriteLine("✅ Generation Complete.");

