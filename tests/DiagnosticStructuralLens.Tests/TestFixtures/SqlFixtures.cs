namespace DiagnosticStructuralLens.Tests.TestFixtures;

/// <summary>
/// Sample T-SQL fixtures for testing the SQL scanner.
/// These represent the patterns we need to capture from real databases.
/// </summary>
public static class SqlFixtures
{
    /// <summary>
    /// Simple table with various column types
    /// </summary>
    public const string UsersTable = """
        CREATE TABLE Users (
            UserId INT PRIMARY KEY IDENTITY(1,1),
            FullName NVARCHAR(100) NOT NULL,
            EmailAddress NVARCHAR(255) NOT NULL,
            DepartmentId INT NULL,
            Status INT NOT NULL DEFAULT 1,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            UpdatedAt DATETIME2 NULL,
            CONSTRAINT FK_Users_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId)
        );
        """;

    /// <summary>
    /// Table with various constraints
    /// </summary>
    public const string OrdersTable = """
        CREATE TABLE Orders (
            OrderId INT PRIMARY KEY IDENTITY(1,1),
            UserId INT NOT NULL,
            TotalAmount DECIMAL(18,2) NOT NULL,
            Status NVARCHAR(50) NOT NULL,
            OrderDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            ShippedDate DATETIME2 NULL,
            CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
            CONSTRAINT CHK_Orders_Amount CHECK (TotalAmount >= 0)
        );
        """;

    /// <summary>
    /// Stored procedure with SELECT - should capture as READ operation
    /// </summary>
    public const string GetUserProc = """
        CREATE PROCEDURE GetUserById
            @UserId INT
        AS
        BEGIN
            SELECT 
                UserId,
                FullName,
                EmailAddress,
                DepartmentId,
                Status,
                CreatedAt
            FROM Users
            WHERE UserId = @UserId;
        END;
        """;

    /// <summary>
    /// Stored procedure with INSERT - should capture as WRITE operation
    /// </summary>
    public const string CreateUserProc = """
        CREATE PROCEDURE CreateUser
            @FullName NVARCHAR(100),
            @EmailAddress NVARCHAR(255),
            @DepartmentId INT = NULL
        AS
        BEGIN
            INSERT INTO Users (FullName, EmailAddress, DepartmentId)
            VALUES (@FullName, @EmailAddress, @DepartmentId);
            
            SELECT SCOPE_IDENTITY() AS NewUserId;
        END;
        """;

    /// <summary>
    /// Stored procedure with UPDATE - should capture as WRITE operation
    /// </summary>
    public const string UpdateUserProc = """
        CREATE PROCEDURE UpdateUser
            @UserId INT,
            @FullName NVARCHAR(100),
            @EmailAddress NVARCHAR(255)
        AS
        BEGIN
            UPDATE Users
            SET 
                FullName = @FullName,
                EmailAddress = @EmailAddress,
                UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId;
        END;
        """;

    /// <summary>
    /// Stored procedure with DELETE - should capture as WRITE operation
    /// </summary>
    public const string DeleteUserProc = """
        CREATE PROCEDURE DeleteUser
            @UserId INT
        AS
        BEGIN
            DELETE FROM Users
            WHERE UserId = @UserId;
        END;
        """;

    /// <summary>
    /// Complex stored procedure with multiple tables and JOINs
    /// </summary>
    public const string GetOrdersWithDetailsProc = """
        CREATE PROCEDURE GetOrdersWithDetails
            @UserId INT,
            @StartDate DATETIME2 = NULL,
            @EndDate DATETIME2 = NULL
        AS
        BEGIN
            SELECT 
                o.OrderId,
                o.TotalAmount,
                o.Status,
                o.OrderDate,
                u.FullName AS CustomerName,
                u.EmailAddress AS CustomerEmail,
                d.Name AS DepartmentName
            FROM Orders o
            INNER JOIN Users u ON o.UserId = u.UserId
            LEFT JOIN Departments d ON u.DepartmentId = d.DepartmentId
            WHERE o.UserId = @UserId
                AND (@StartDate IS NULL OR o.OrderDate >= @StartDate)
                AND (@EndDate IS NULL OR o.OrderDate <= @EndDate)
            ORDER BY o.OrderDate DESC;
        END;
        """;

    /// <summary>
    /// View referencing multiple tables
    /// </summary>
    public const string UserSummaryView = """
        CREATE VIEW vw_UserSummary AS
        SELECT 
            u.UserId,
            u.FullName,
            u.EmailAddress,
            d.Name AS DepartmentName,
            (SELECT COUNT(*) FROM Orders o WHERE o.UserId = u.UserId) AS OrderCount,
            (SELECT SUM(TotalAmount) FROM Orders o WHERE o.UserId = u.UserId) AS TotalSpent
        FROM Users u
        LEFT JOIN Departments d ON u.DepartmentId = d.DepartmentId;
        """;

    /// <summary>
    /// Table-valued function
    /// </summary>
    public const string GetUserOrdersFunction = """
        CREATE FUNCTION fn_GetUserOrders(@UserId INT)
        RETURNS TABLE
        AS
        RETURN
        (
            SELECT 
                OrderId,
                TotalAmount,
                Status,
                OrderDate
            FROM Orders
            WHERE UserId = @UserId
        );
        """;

    /// <summary>
    /// Multiple statements in one script (common in migration files)
    /// </summary>
    public const string MultiStatementScript = """
        CREATE TABLE Departments (
            DepartmentId INT PRIMARY KEY IDENTITY(1,1),
            Name NVARCHAR(100) NOT NULL,
            Code NVARCHAR(10) NOT NULL UNIQUE
        );
        GO
        
        CREATE TABLE Roles (
            RoleId INT PRIMARY KEY IDENTITY(1,1),
            Name NVARCHAR(50) NOT NULL,
            Description NVARCHAR(255) NULL
        );
        GO
        
        CREATE TABLE UserRoles (
            UserId INT NOT NULL,
            RoleId INT NOT NULL,
            PRIMARY KEY (UserId, RoleId),
            FOREIGN KEY (UserId) REFERENCES Users(UserId),
            FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
        );
        """;
}
