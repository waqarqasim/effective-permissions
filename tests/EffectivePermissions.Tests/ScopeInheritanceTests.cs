namespace EffectivePermissions.Tests;

public sealed class ScopeInheritanceTests
{
    [Fact]
    public void A_grant_applies_to_everything_beneath_it()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.read", "north"));

        subject.IsAllowed("orders.read", "north").ShouldBeTrue();
        subject.IsAllowed("orders.read", "leeds").ShouldBeTrue();
        subject.IsAllowed("orders.read", "york").ShouldBeTrue();
    }

    [Fact]
    public void A_grant_does_not_apply_to_a_sibling()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.read", "north"));

        subject.IsAllowed("orders.read", "south").ShouldBeFalse();
        subject.IsAllowed("orders.read", "bristol").ShouldBeFalse();
    }

    [Fact]
    public void A_grant_does_not_apply_upwards()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.read", "leeds"));

        // Someone who can read orders at one warehouse cannot read them for the region.
        // Inheritance running upwards is how a warehouse supervisor quietly becomes an
        // administrator.
        subject.IsAllowed("orders.read", "north").ShouldBeFalse();
        subject.IsAllowed("orders.read", "acme").ShouldBeFalse();
    }

    [Fact]
    public void A_grant_at_the_root_reaches_every_leaf()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.read", "acme"));

        Depot.Scopes.Nodes
            .Select(n => subject.IsAllowed("orders.read", n.Id))
            .ShouldAllBe(allowed => allowed);
    }

    [Fact]
    public void The_scopes_a_permission_is_held_at_can_be_listed()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.read", "north"),
            Grant.Allow("orders.read", "bristol"));

        // Everything under north, plus bristol — but not south, which only contains bristol.
        subject.ScopesAllowing("orders.read")
            .ShouldBe(["bristol", "leeds", "north", "york"], ignoreOrder: true);
    }

    [Fact]
    public void Asking_about_a_scope_that_does_not_exist_throws_rather_than_denying()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.read", "acme"));

        // Returning false here would present a caller's typo as an access-denied page, and
        // the person debugging it would go looking at grants.
        Should.Throw<KeyNotFoundException>(() => subject.IsAllowed("orders.read", "atlantis"));
    }

    [Fact]
    public void A_scope_naming_a_parent_that_does_not_exist_is_refused_at_construction()
    {
        var act = () => new ScopeTree(
        [
            new ScopeNode("acme", "business", null),
            new ScopeNode("orphan", "warehouse", "nowhere"),
        ]);

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("applies to nothing");
    }

    [Fact]
    public void A_cycle_in_the_hierarchy_is_refused_at_construction()
    {
        var act = () => new ScopeTree(
        [
            new ScopeNode("a", "node", "b"),
            new ScopeNode("b", "node", "a"),
        ]);

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("cycle");
    }
}
