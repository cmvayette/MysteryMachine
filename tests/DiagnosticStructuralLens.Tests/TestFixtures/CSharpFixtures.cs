namespace DiagnosticStructuralLens.Tests.TestFixtures;

/// <summary>
/// Sample C# code fixtures for testing the scanner.
/// These represent the patterns we need to capture from real codebases.
/// </summary>
public static class CSharpFixtures
{
    /// <summary>
    /// DTO with naming convention - should be classified as AtomType.Dto
    /// </summary>
    public const string SimpleDto = """
        namespace Company.Models
        {
            public class UserDTO
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string Email { get; set; }
            }
        }
        """;

    /// <summary>
    /// DTO with [DataContract] attribute - should be classified as AtomType.Dto
    /// </summary>
    public const string DataContractDto = """
        using System.Runtime.Serialization;
        
        namespace Company.Contracts
        {
            [DataContract]
            public class OrderRequest
            {
                [DataMember]
                public int OrderId { get; set; }
                
                [DataMember]
                public decimal Amount { get; set; }
            }
        }
        """;

    /// <summary>
    /// EF Core entity with table/column attributes - need to extract mappings
    /// </summary>
    public const string EfCoreEntity = """
        using System.ComponentModel.DataAnnotations;
        using System.ComponentModel.DataAnnotations.Schema;
        
        namespace Company.Data.Entities
        {
            [Table("Users")]
            public class UserEntity
            {
                [Key]
                [Column("UserId")]
                public int Id { get; set; }
                
                [Column("FullName")]
                [Required]
                [StringLength(100)]
                public string Name { get; set; }
                
                [Column("EmailAddress")]
                public string Email { get; set; }
                
                [ForeignKey("DepartmentId")]
                public Department Department { get; set; }
            }
        }
        """;

    /// <summary>
    /// Interface with method signatures - should capture as contract
    /// </summary>
    public const string ServiceInterface = """
        namespace Company.Services
        {
            public interface IUserService
            {
                Task<UserDTO> GetByIdAsync(int id);
                Task<IEnumerable<UserDTO>> GetAllAsync();
                Task<UserDTO> CreateAsync(CreateUserRequest request);
                Task UpdateAsync(int id, UpdateUserRequest request);
                Task DeleteAsync(int id);
            }
        }
        """;

    /// <summary>
    /// Class with Dapper inline SQL - should extract query for linking
    /// </summary>
    public const string DapperRepository = """
        using Dapper;
        using System.Data;
        
        namespace Company.Data.Repositories
        {
            public class UserRepository
            {
                private readonly IDbConnection _connection;
                
                public async Task<User> GetByIdAsync(int id)
                {
                    return await _connection.QueryFirstOrDefaultAsync<User>(
                        "SELECT UserId, FullName, EmailAddress FROM Users WHERE UserId = @Id",
                        new { Id = id });
                }
                
                public async Task<IEnumerable<User>> GetByDepartmentAsync(int deptId)
                {
                    return await _connection.QueryAsync<User>(
                        @"SELECT u.UserId, u.FullName, u.EmailAddress, d.Name as DepartmentName
                          FROM Users u
                          INNER JOIN Departments d ON u.DepartmentId = d.DepartmentId
                          WHERE d.DepartmentId = @DeptId",
                        new { DeptId = deptId });
                }
            }
        }
        """;

    /// <summary>
    /// Request/Response DTOs - common pattern
    /// </summary>
    public const string RequestResponseDtos = """
        namespace Company.Contracts.Requests
        {
            public record CreateUserRequest(string Name, string Email, int DepartmentId);
            public record UpdateUserRequest(string Name, string Email);
        }
        
        namespace Company.Contracts.Responses
        {
            public record UserResponse(int Id, string Name, string Email, string DepartmentName);
            public record PagedResponse<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
        }
        """;

    /// <summary>
    /// Enum that should be captured
    /// </summary>
    public const string EnumDefinition = """
        namespace Company.Models
        {
            public enum UserStatus
            {
                Active = 1,
                Inactive = 2,
                Suspended = 3,
                Deleted = 4
            }
        }
        """;

    /// <summary>
    /// Private class that should NOT be captured (unless configured)
    /// </summary>
    public const string InternalClass = """
        namespace Company.Internal
        {
            internal class CacheHelper
            {
                private readonly Dictionary<string, object> _cache = new();
                
                public T Get<T>(string key) => (T)_cache[key];
                public void Set(string key, object value) => _cache[key] = value;
            }
        }
        """;
}
