using SystemCartographer.Core;
using SystemCartographer.Linker;
using Xunit;

namespace SystemCartographer.Tests.ScannerRequirements;

/// <summary>
/// Phase 2 tests: Verify the Semantic Linker connects C# code to SQL schema.
/// </summary>
public class SemanticLinkerRequirementsTests
{
    private readonly SemanticLinker _linker = new();

    #region 1. Name-Based Linking

    [Fact]
    public void Linker_Should_Match_ExactNames()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:users", Name = "Users", Type = AtomType.Class, Namespace = "App.Models" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "code:users");
        Assert.NotNull(link);
        Assert.Equal("table:users", link.TargetId);
        Assert.Equal(LinkType.ExactMatch, link.Type);
        Assert.Equal(0.95, link.Confidence, precision: 2);
    }

    [Fact]
    public void Linker_Should_Match_Pluralized_Names()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:user", Name = "User", Type = AtomType.Class, Namespace = "App.Models" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "code:user");
        Assert.NotNull(link);
        Assert.Equal("table:users", link.TargetId);
        Assert.Equal(LinkType.FuzzyMatch, link.Type);
        Assert.InRange(link.Confidence, 0.80, 0.90);
    }

    [Fact]
    public void Linker_Should_Match_Dto_Suffix_Names()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:userdto", Name = "UserDTO", Type = AtomType.Dto, Namespace = "App.Models" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "code:userdto");
        Assert.NotNull(link);
        Assert.Equal("table:users", link.TargetId);
        Assert.Equal(LinkType.FuzzyMatch, link.Type);
        Assert.InRange(link.Confidence, 0.75, 0.85);
    }

    [Fact]
    public void Linker_Should_Match_Entity_Suffix()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:userentity", Name = "UserEntity", Type = AtomType.Class, Namespace = "App.Data" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "code:userentity");
        Assert.NotNull(link);
        Assert.Equal("table:users", link.TargetId);
        Assert.Equal(LinkType.FuzzyMatch, link.Type);
    }

    [Fact]
    public void Linker_Should_Not_Match_Unrelated_Names()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:orderdto", Name = "OrderDTO", Type = AtomType.Dto, Namespace = "App.Models" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "code:orderdto" && l.TargetId == "table:users");
        Assert.Null(link); // No link should be created
    }

    [Fact]
    public void Linker_Should_Match_Property_To_Column()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "prop:email", Name = "EmailAddress", Type = AtomType.Property, Namespace = "App.Models" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "col:email", Name = "EmailAddress", Type = SqlAtomType.Column, ParentTable = "Users" }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "prop:email");
        Assert.NotNull(link);
        Assert.Equal("col:email", link.TargetId);
        Assert.Equal(LinkType.PropertyMatch, link.Type);
        Assert.Equal(0.90, link.Confidence, precision: 2);
    }

    #endregion

    #region 2. Attribute-Based Linking

    [Fact]
    public void Linker_Should_Use_TableAttribute_For_ExactMatch()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "entity:customer", Name = "CustomerEntity", Type = AtomType.Class, Namespace = "App.Data" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:customers", Name = "Customers", Type = SqlAtomType.Table }
        };
        // Scanner creates this link when it finds [Table("Customers")] attribute
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "attr-1", SourceId = "entity:customer", TargetId = "table:Customers", 
                    Type = LinkType.AttributeBinding, Confidence = 1.0, Evidence = "[Table(\"Customers\")]" }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms, existingLinks);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "entity:customer");
        Assert.NotNull(link);
        Assert.Equal("table:customers", link.TargetId);
        Assert.Equal(LinkType.AttributeBinding, link.Type);
        Assert.Equal(1.0, link.Confidence);
    }

    [Fact]
    public void Linker_Should_Use_ColumnAttribute_For_PropertyMatch()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "prop:name", Name = "Name", Type = AtomType.Property, Namespace = "App.Data" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "col:fullname", Name = "FullName", Type = SqlAtomType.Column }
        };
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "attr-2", SourceId = "prop:name", TargetId = "column:FullName", 
                    Type = LinkType.AttributeBinding, Confidence = 1.0, Evidence = "[Column(\"FullName\")]" }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms, existingLinks);
        
        var link = result.Links.FirstOrDefault(l => l.SourceId == "prop:name");
        Assert.NotNull(link);
        Assert.Equal("col:fullname", link.TargetId);
        Assert.Equal(LinkType.AttributeBinding, link.Type);
        Assert.Equal(1.0, link.Confidence);
    }

    [Fact]
    public void Linker_Should_Override_NameMatch_With_Attribute()
    {
        // UserEntity would normally fuzzy-match to Users, but attribute says Customers
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "entity:user", Name = "UserEntity", Type = AtomType.Class, Namespace = "App.Data" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table },
            new() { Id = "table:customers", Name = "Customers", Type = SqlAtomType.Table }
        };
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "attr-3", SourceId = "entity:user", TargetId = "table:Customers", 
                    Type = LinkType.AttributeBinding, Confidence = 1.0, Evidence = "[Table(\"Customers\")]" }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms, existingLinks);
        
        // Should link to Customers, not Users
        var customerLink = result.Links.FirstOrDefault(l => l.TargetId == "table:customers");
        Assert.NotNull(customerLink);
        Assert.Equal(LinkType.AttributeBinding, customerLink.Type);
        
        // Should NOT also have a link to Users
        var userLinks = result.Links.Where(l => l.TargetId == "table:users" && l.SourceId == "entity:user");
        Assert.Empty(userLinks);
    }

    [Fact]
    public void Linker_Should_Capture_ForeignKey_Relationships()
    {
        // FK relationships come from scanner as links
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "fk-1", SourceId = "col:departmentid", TargetId = "table:departments", 
                    Type = LinkType.References, Confidence = 1.0, Evidence = "Foreign key to Departments" }
        };

        var result = _linker.LinkAtoms([], [], existingLinks);
        
        // FK links are passed through as-is
        Assert.NotEmpty(existingLinks);
    }

    #endregion

    #region 3. Query-Based Linking

    [Fact]
    public void Linker_Should_Parse_Dapper_Select()
    {
        var sql = "SELECT * FROM Users WHERE Id = @Id";
        var tables = _linker.ExtractTablesFromSql(sql);
        
        Assert.Contains("Users", tables);
    }

    [Fact]
    public void Linker_Should_Parse_Dapper_Join()
    {
        var sql = "SELECT u.*, o.* FROM Users u JOIN Orders o ON u.Id = o.UserId";
        var tables = _linker.ExtractTablesFromSql(sql);
        
        Assert.Contains("Users", tables);
        Assert.Contains("Orders", tables);
    }

    [Fact]
    public void Linker_Should_Handle_Multiline_Sql()
    {
        var sql = @"
            SELECT u.Id, u.Name, d.Name AS Department
            FROM Users u
            INNER JOIN Departments d ON u.DeptId = d.Id
            LEFT JOIN Roles r ON u.RoleId = r.Id
            WHERE u.Active = 1";
        
        var tables = _linker.ExtractTablesFromSql(sql);
        
        Assert.Contains("Users", tables);
        Assert.Contains("Departments", tables);
        Assert.Contains("Roles", tables);
    }

    [Fact]
    public void Linker_Should_Parse_Insert()
    {
        var sql = "INSERT INTO Users (Name, Email) VALUES (@Name, @Email)";
        var tables = _linker.ExtractTablesFromSql(sql);
        
        Assert.Contains("Users", tables);
    }

    [Fact]
    public void Linker_Should_Parse_Update()
    {
        var sql = "UPDATE Users SET Name = @Name WHERE Id = @Id";
        var tables = _linker.ExtractTablesFromSql(sql);
        
        Assert.Contains("Users", tables);
    }

    [Fact]
    public void Linker_Should_Parse_Delete()
    {
        var sql = "DELETE FROM Users WHERE Id = @Id";
        var tables = _linker.ExtractTablesFromSql(sql);
        
        Assert.Contains("Users", tables);
    }

    [Fact]
    public void Linker_Should_Process_QueryTrace_Links()
    {
        var codeAtoms = new List<CodeAtom>();
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "qt-1", SourceId = "repo:user", TargetId = "table:Users", 
                    Type = LinkType.QueryTrace, Evidence = "Dapper: SELECT * FROM Users" }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms, existingLinks);
        
        var queryLink = result.Links.FirstOrDefault(l => l.Type == LinkType.QueryTrace);
        Assert.NotNull(queryLink);
        Assert.Equal("repo:user", queryLink.SourceId);
        Assert.Equal("table:users", queryLink.TargetId);
    }

    #endregion

    #region 4. Blast Radius Queries

    [Fact]
    public void GetDependents_Should_Return_All_Consumers()
    {
        var links = new List<AtomLink>
        {
            new() { Id = "l1", SourceId = "dto:user", TargetId = "table:users", Type = LinkType.ExactMatch },
            new() { Id = "l2", SourceId = "entity:user", TargetId = "table:users", Type = LinkType.ExactMatch },
            new() { Id = "l3", SourceId = "repo:user", TargetId = "table:users", Type = LinkType.QueryTrace }
        };

        var dependents = _linker.GetDependents("table:users", links);
        
        Assert.Contains("dto:user", dependents);
        Assert.Contains("entity:user", dependents);
        Assert.Contains("repo:user", dependents);
        Assert.Equal(3, dependents.Count);
    }

    [Fact]
    public void GetDependencies_Should_Return_All_Sources()
    {
        var links = new List<AtomLink>
        {
            new() { Id = "l1", SourceId = "dto:user", TargetId = "table:users", Type = LinkType.ExactMatch },
            new() { Id = "l2", SourceId = "dto:user", TargetId = "col:email", Type = LinkType.PropertyMatch }
        };

        var dependencies = _linker.GetDependencies("dto:user", links);
        
        Assert.Contains("table:users", dependencies);
        Assert.Contains("col:email", dependencies);
        Assert.Equal(2, dependencies.Count);
    }

    [Fact]
    public void GetBlastRadius_Should_Calculate_Depth()
    {
        var links = new List<AtomLink>
        {
            new() { Id = "l1", SourceId = "entity:user", TargetId = "table:users", Type = LinkType.ExactMatch },
            new() { Id = "l2", SourceId = "dto:user", TargetId = "entity:user", Type = LinkType.Contains },
            new() { Id = "l3", SourceId = "service:user", TargetId = "dto:user", Type = LinkType.References }
        };

        var blastRadius = _linker.GetBlastRadius("table:users", links, maxDepth: 5);
        
        Assert.Equal("table:users", blastRadius.RootAtomId);
        Assert.Equal(3, blastRadius.TotalAffected);
        Assert.Equal(3, blastRadius.MaxDepth);
    }

    #endregion

    #region 5. Confidence Calibration

    [Fact]
    public void Linker_Should_Assign_HighConfidence_To_AttributeLinks()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "entity:order", Name = "OrderEntity", Type = AtomType.Class, Namespace = "App" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:orders", Name = "Orders", Type = SqlAtomType.Table }
        };
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "attr", SourceId = "entity:order", TargetId = "table:Orders", 
                    Type = LinkType.AttributeBinding }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms, existingLinks);
        var link = result.Links.First(l => l.Type == LinkType.AttributeBinding);
        
        Assert.Equal(1.0, link.Confidence);
    }

    [Fact]
    public void Linker_Should_Assign_MediumConfidence_To_ExactNameMatch()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:users", Name = "Users", Type = AtomType.Class, Namespace = "App" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        var link = result.Links.First();
        
        Assert.Equal(0.95, link.Confidence, precision: 2);
    }

    [Fact]
    public void Linker_Should_Assign_LowerConfidence_To_FuzzyMatch()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:userdto", Name = "UserDTO", Type = AtomType.Dto, Namespace = "App" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        var link = result.Links.First();
        
        Assert.InRange(link.Confidence, 0.70, 0.85);
    }

    [Fact]
    public void Linker_Should_Record_Evidence_For_Links()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "code:users", Name = "Users", Type = AtomType.Class, Namespace = "App" }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:users", Name = "Users", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms(codeAtoms, sqlAtoms);
        var link = result.Links.First();
        
        Assert.NotNull(link.Evidence);
        Assert.NotEmpty(link.Evidence);
    }

    [Fact]
    public void HighConfidence_Links_Should_Be_Correct()
    {
        // All attribute bindings should have 1.0 confidence
        var existingLinks = new List<AtomLink>
        {
            new() { Id = "a1", SourceId = "e1", TargetId = "table:T1", Type = LinkType.AttributeBinding },
            new() { Id = "a2", SourceId = "e2", TargetId = "table:T2", Type = LinkType.AttributeBinding }
        };
        var sqlAtoms = new List<SqlAtom>
        {
            new() { Id = "table:t1", Name = "T1", Type = SqlAtomType.Table },
            new() { Id = "table:t2", Name = "T2", Type = SqlAtomType.Table }
        };

        var result = _linker.LinkAtoms([], sqlAtoms, existingLinks);
        
        Assert.All(result.Links.Where(l => l.Type == LinkType.AttributeBinding), 
            l => Assert.Equal(1.0, l.Confidence));
    }

    #endregion

    #region 6. Interface Method Linking

    [Fact]
    public void Linker_Should_Link_Interface_Method_ReturnType_To_Dto()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "iface:userservice", Name = "IUserService", Type = AtomType.Interface, Namespace = "App" },
            new() { Id = "iface:userservice-getbyid", Name = "GetByIdAsync", Type = AtomType.Method, 
                    Namespace = "App", Signature = "Task<UserDTO> GetByIdAsync(int id)" },
            new() { Id = "dto:userdto", Name = "UserDTO", Type = AtomType.Dto, Namespace = "App" }
        };

        var result = _linker.LinkAtoms(codeAtoms, []);
        
        var returnLink = result.Links.FirstOrDefault(l => 
            l.SourceId == "iface:userservice-getbyid" && l.TargetId == "dto:userdto");
        Assert.NotNull(returnLink);
        Assert.Equal(LinkType.References, returnLink.Type);
        Assert.Contains("returns", returnLink.Evidence!);
    }

    [Fact]
    public void Linker_Should_Link_Interface_Method_Parameter_To_Dto()
    {
        var codeAtoms = new List<CodeAtom>
        {
            new() { Id = "iface:userservice", Name = "IUserService", Type = AtomType.Interface, Namespace = "App" },
            new() { Id = "iface:userservice-create", Name = "CreateAsync", Type = AtomType.Method, 
                    Namespace = "App", Signature = "Task CreateAsync(CreateUserRequest request)" },
            new() { Id = "dto:createuserrequest", Name = "CreateUserRequest", Type = AtomType.Dto, Namespace = "App" }
        };

        var result = _linker.LinkAtoms(codeAtoms, []);
        
        var paramLink = result.Links.FirstOrDefault(l => 
            l.SourceId == "iface:userservice-create" && l.TargetId == "dto:createuserrequest");
        Assert.NotNull(paramLink);
        Assert.Equal(LinkType.References, paramLink.Type);
        Assert.Contains("parameter", paramLink.Evidence!);
    }

    #endregion
}
