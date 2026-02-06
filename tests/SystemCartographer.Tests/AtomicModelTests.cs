using SystemCartographer.Core;
using Xunit;

namespace SystemCartographer.Tests;

public class AtomicModelTests
{
    [Fact]
    public void CodeAtom_CanBeCreated()
    {
        var atom = new CodeAtom
        {
            Id = "test-1",
            Name = "UserDTO",
            Type = AtomType.Dto,
            Namespace = "Company.Models"
        };

        Assert.Equal("UserDTO", atom.Name);
        Assert.Equal(AtomType.Dto, atom.Type);
    }

    [Fact]
    public void SqlAtom_CanBeCreated()
    {
        var atom = new SqlAtom
        {
            Id = "sql-1",
            Name = "Users",
            Type = SqlAtomType.Table
        };

        Assert.Equal("Users", atom.Name);
        Assert.Equal(SqlAtomType.Table, atom.Type);
    }

    [Fact]
    public void AtomLink_CanConnectAtoms()
    {
        var link = new AtomLink
        {
            Id = "link-1",
            SourceId = "test-1",
            TargetId = "sql-1",
            Type = LinkType.NameMatch,
            Confidence = 0.95
        };

        Assert.Equal(LinkType.NameMatch, link.Type);
        Assert.Equal(0.95, link.Confidence);
    }

    [Fact]
    public void Snapshot_ContainsAtoms()
    {
        var snapshot = new Snapshot
        {
            Id = "snap-1",
            Repository = "/path/to/repo",
            ScannedAt = DateTimeOffset.UtcNow,
            CodeAtoms = 
            [
                new CodeAtom { Id = "c1", Name = "Test", Type = AtomType.Class, Namespace = "Test" }
            ],
            SqlAtoms =
            [
                new SqlAtom { Id = "s1", Name = "TestTable", Type = SqlAtomType.Table }
            ]
        };

        Assert.Single(snapshot.CodeAtoms);
        Assert.Single(snapshot.SqlAtoms);
    }
}
