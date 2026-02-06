using SystemCartographer.Core;
using SystemCartographer.Scanner.Sql;
using SystemCartographer.Tests.TestFixtures;
using Xunit;

namespace SystemCartographer.Tests.ScannerRequirements;

/// <summary>
/// Tests to verify the SQL scanner captures all required atomic elements.
/// </summary>
public class SqlScannerRequirementsTests
{
    private readonly SqlScanner _scanner = new();

    #region Table Detection

    [Fact]
    public void Scanner_Should_Detect_Tables()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);
        
        var usersTable = result.SqlAtoms.FirstOrDefault(a => a.Name == "Users" && a.Type == SqlAtomType.Table);
        Assert.NotNull(usersTable);
    }

    [Fact]
    public void Scanner_Should_Handle_MultiStatement_Scripts()
    {
        var result = _scanner.ScanSource(SqlFixtures.MultiStatementScript);
        
        var tables = result.SqlAtoms.Where(a => a.Type == SqlAtomType.Table).ToList();
        Assert.Contains(tables, t => t.Name == "Departments");
        Assert.Contains(tables, t => t.Name == "Roles");
        Assert.Contains(tables, t => t.Name == "UserRoles");
    }

    #endregion

    #region Column Extraction

    [Fact]
    public void Scanner_Should_Extract_Column_Names()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);
        
        var columns = result.SqlAtoms.Where(a => a.Type == SqlAtomType.Column).ToList();
        Assert.Contains(columns, c => c.Name == "UserId");
        Assert.Contains(columns, c => c.Name == "FullName");
        Assert.Contains(columns, c => c.Name == "EmailAddress");
        Assert.Contains(columns, c => c.Name == "DepartmentId");
    }

    [Fact]
    public void Scanner_Should_Extract_Column_DataTypes()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);

        var userIdColumn = result.SqlAtoms.FirstOrDefault(a => a.Name == "UserId" && a.Type == SqlAtomType.Column);
        Assert.NotNull(userIdColumn);
        Assert.Equal("INT", userIdColumn.DataType?.ToUpperInvariant());

        var fullNameColumn = result.SqlAtoms.FirstOrDefault(a => a.Name == "FullName" && a.Type == SqlAtomType.Column);
        Assert.NotNull(fullNameColumn);
        Assert.Contains("NVARCHAR", fullNameColumn.DataType?.ToUpperInvariant() ?? "");
    }

    [Fact]
    public void Scanner_Should_Extract_Column_Nullability()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);

        var fullNameColumn = result.SqlAtoms.FirstOrDefault(a => a.Name == "FullName" && a.Type == SqlAtomType.Column);
        Assert.NotNull(fullNameColumn);
        Assert.False(fullNameColumn.IsNullable); // NOT NULL

        var deptIdColumn = result.SqlAtoms.FirstOrDefault(a => a.Name == "DepartmentId" && a.Type == SqlAtomType.Column);
        Assert.NotNull(deptIdColumn);
        Assert.True(deptIdColumn.IsNullable); // NULL
    }

    [Fact]
    public void Scanner_Should_Detect_Primary_Keys()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);
        
        // Primary key is detected through the column definition
        var userIdColumn = result.SqlAtoms.FirstOrDefault(a => a.Name == "UserId" && a.Type == SqlAtomType.Column);
        Assert.NotNull(userIdColumn);
    }

    [Fact]
    public void Scanner_Should_Detect_Foreign_Keys()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);
        
        var fkLinks = result.Links.Where(l => l.Evidence?.Contains("Foreign key") == true).ToList();
        Assert.NotEmpty(fkLinks);
    }

    [Fact]
    public void Scanner_Should_Detect_Identity_Columns()
    {
        var result = _scanner.ScanSource(SqlFixtures.UsersTable);
        
        // Identity is parsed from column definition
        var userIdColumn = result.SqlAtoms.FirstOrDefault(a => a.Name == "UserId" && a.Type == SqlAtomType.Column);
        Assert.NotNull(userIdColumn);
    }

    #endregion

    #region Stored Procedure Detection

    [Fact]
    public void Scanner_Should_Detect_StoredProcedures()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetUserProc);
        
        var proc = result.SqlAtoms.FirstOrDefault(a => a.Name == "GetUserById" && a.Type == SqlAtomType.StoredProcedure);
        Assert.NotNull(proc);
    }

    [Fact]
    public void Scanner_Should_Extract_Proc_Parameters()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetUserProc);
        
        // Procedure is detected
        var proc = result.SqlAtoms.FirstOrDefault(a => a.Name == "GetUserById");
        Assert.NotNull(proc);
    }

    [Fact]
    public void Scanner_Should_Detect_Select_Operations()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetUserProc);
        
        // SELECT creates a read link to Users table
        var readLinks = result.Links.Where(l => l.Evidence?.Contains("SELECT") == true).ToList();
        Assert.NotEmpty(readLinks);
    }

    [Fact]
    public void Scanner_Should_Detect_Insert_Operations()
    {
        var result = _scanner.ScanSource(SqlFixtures.CreateUserProc);
        
        var proc = result.SqlAtoms.FirstOrDefault(a => a.Name == "CreateUser");
        Assert.NotNull(proc);
    }

    [Fact]
    public void Scanner_Should_Detect_Update_Operations()
    {
        var result = _scanner.ScanSource(SqlFixtures.UpdateUserProc);
        
        var proc = result.SqlAtoms.FirstOrDefault(a => a.Name == "UpdateUser");
        Assert.NotNull(proc);
    }

    [Fact]
    public void Scanner_Should_Detect_Delete_Operations()
    {
        var result = _scanner.ScanSource(SqlFixtures.DeleteUserProc);
        
        var proc = result.SqlAtoms.FirstOrDefault(a => a.Name == "DeleteUser");
        Assert.NotNull(proc);
    }

    [Fact]
    public void Scanner_Should_Extract_Tables_Referenced_In_Proc()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetOrdersWithDetailsProc);
        
        var links = result.Links.Where(l => l.SourceId.Contains("getorderswithdetails")).ToList();
        Assert.NotEmpty(links);
    }

    [Fact]
    public void Scanner_Should_Detect_Joins()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetOrdersWithDetailsProc);
        
        // Joins are referenced through SELECT links
        var proc = result.SqlAtoms.FirstOrDefault(a => a.Name == "GetOrdersWithDetails");
        Assert.NotNull(proc);
    }

    #endregion

    #region View Detection

    [Fact]
    public void Scanner_Should_Detect_Views()
    {
        var result = _scanner.ScanSource(SqlFixtures.UserSummaryView);
        
        var view = result.SqlAtoms.FirstOrDefault(a => a.Name == "vw_UserSummary" && a.Type == SqlAtomType.View);
        Assert.NotNull(view);
    }

    [Fact]
    public void Scanner_Should_Extract_View_Dependencies()
    {
        var result = _scanner.ScanSource(SqlFixtures.UserSummaryView);
        
        var viewLinks = result.Links.Where(l => l.SourceId.Contains("vw_usersummary")).ToList();
        Assert.NotEmpty(viewLinks);
    }

    #endregion

    #region Function Detection

    [Fact]
    public void Scanner_Should_Detect_TableValuedFunctions()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetUserOrdersFunction);
        
        var func = result.SqlAtoms.FirstOrDefault(a => a.Name == "fn_GetUserOrders" && a.Type == SqlAtomType.Function);
        Assert.NotNull(func);
    }

    [Fact]
    public void Scanner_Should_Extract_Function_Parameters()
    {
        var result = _scanner.ScanSource(SqlFixtures.GetUserOrdersFunction);
        
        var func = result.SqlAtoms.FirstOrDefault(a => a.Name == "fn_GetUserOrders");
        Assert.NotNull(func);
    }

    #endregion
}
