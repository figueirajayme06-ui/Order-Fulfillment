using FluentAssertions;
using OF.Data.Database;
using OF.WebApp.Features.Divisions;

namespace OF.Tests.WebApp.Features.Divisions;

public class DivisionAccessTests
{
    [Fact]
    public void Resolve_UsesAllAssignedDivisions_WhenNormalUserDoesNotRequestOne()
    {
        var identity = CreateIdentity(" uk, IE,uk ");

        DivisionAccess.Resolve(identity, null, [',']).Should().Equal("UK", "IE");
    }

    [Fact]
    public void Resolve_ReturnsNoDivisions_WhenNormalUserExplicitlyRequestsOnlyDelimiters()
    {
        var identity = CreateIdentity("UK,IE");

        DivisionAccess.Resolve(identity, " , ", [',']).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_IntersectsRequestedDivisionsIgnoringCase_ForNormalUser()
    {
        var identity = CreateIdentity("UK,IE");

        DivisionAccess.Resolve(identity, " ie ; fr ; IE ", [',', ';']).Should().Equal("IE");
    }

    [Fact]
    public void Resolve_ReturnsNoDivisions_WhenNormalUserHasNone()
    {
        DivisionAccess.Resolve(CreateIdentity(""), "UK", [',']).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_AllowsRequestedDivisions_ForSuperAdmin()
    {
        DivisionAccess.Resolve(CreateIdentity("", isSuperAdmin: true), "uk;fr", [',', ';'])
            .Should().Equal("UK", "FR");
    }

    [Theory]
    [InlineData("UK", "uk", true)]
    [InlineData("UK", "FR", false)]
    [InlineData("", "UK", false)]
    [InlineData("UK", null, false)]
    public void CanAccess_RequiresAnAssignedMatchingDivision_ForNormalUser(
        string assignedDivisions,
        string? resourceDivision,
        bool expected)
    {
        DivisionAccess.CanAccess(CreateIdentity(assignedDivisions), resourceDivision).Should().Be(expected);
    }

    [Fact]
    public void CanAccess_DoesNotRestrictSuperAdmin()
    {
        DivisionAccess.CanAccess(CreateIdentity("", isSuperAdmin: true), null).Should().BeTrue();
    }

    [Theory]
    [InlineData("UK,IE", " fr, ie ", true)]
    [InlineData("UK,IE", "FR,DE", false)]
    [InlineData("", "UK", false)]
    [InlineData("UK", "", false)]
    public void CanAccessAny_RequiresAtLeastOneMatchingDivision_ForNormalUsers(
        string assignedDivisions,
        string resourceDivisions,
        bool expected)
    {
        DivisionAccess.CanAccessAny(CreateIdentity(assignedDivisions), resourceDivisions, [','])
            .Should().Be(expected);
    }

    [Fact]
    public void CanAccessAny_DoesNotRestrictSuperAdminWhenResourceHasNoDivision()
    {
        DivisionAccess.CanAccessAny(CreateIdentity("", isSuperAdmin: true), null, [',']).Should().BeTrue();
    }

    [Theory]
    [InlineData("UK,IE", " ie, uk,IE ", true)]
    [InlineData("UK,IE", "UK,FR", false)]
    [InlineData("", "UK", false)]
    [InlineData("UK", "", false)]
    public void CanAssignAll_RequiresEveryRequestedDivision_ForNormalUsers(
        string assignedDivisions,
        string resourceDivisions,
        bool expected)
    {
        DivisionAccess.CanAssignAll(CreateIdentity(assignedDivisions), resourceDivisions, [','])
            .Should().Be(expected);
    }

    [Fact]
    public void CanAssignAll_DoesNotRestrictSuperAdminWhenNoDivisionIsRequested()
    {
        DivisionAccess.CanAssignAll(CreateIdentity("", isSuperAdmin: true), null, [',']).Should().BeTrue();
    }

    private static User CreateIdentity(string divisions, bool isSuperAdmin = false) => new()
    {
        LoginName = "planner@example.com",
        FullName = "Fleet Planner",
        Division = divisions,
        IsSuperAdmin = isSuperAdmin,
        DateFormat = "dd/MM/yyyy",
    };
}
